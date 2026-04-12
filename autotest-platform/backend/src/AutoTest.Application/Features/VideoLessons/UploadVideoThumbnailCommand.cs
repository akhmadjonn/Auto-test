using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.VideoLessons;

public record UploadVideoThumbnailCommand(Guid LessonId, Stream Image, string FileName) : IRequest<ApiResponse<string>>;

public class UploadVideoThumbnailCommandValidator : AbstractValidator<UploadVideoThumbnailCommand>
{
    public UploadVideoThumbnailCommandValidator()
    {
        RuleFor(x => x.LessonId).NotEmpty();
        RuleFor(x => x.Image).NotNull();
        RuleFor(x => x.FileName).NotEmpty();
    }
}

public class UploadVideoThumbnailCommandHandler(
    IApplicationDbContext db,
    IImageProcessingService imageProcessor,
    IFileStorageService storage,
    IDateTimeProvider dateTime,
    ICacheService cache,
    ILogger<UploadVideoThumbnailCommandHandler> logger) : IRequestHandler<UploadVideoThumbnailCommand, ApiResponse<string>>
{
    public async Task<ApiResponse<string>> Handle(UploadVideoThumbnailCommand request, CancellationToken ct)
    {
        var lesson = await db.VideoLessons.FindAsync([request.LessonId], ct);
        if (lesson is null)
            return ApiResponse<string>.Fail("NOT_FOUND", "Video lesson not found.");

        var oldThumbnailKey = lesson.ThumbnailUrl;

        var processed = await imageProcessor.ProcessImageAsync(request.Image, request.FileName, ct);
        var fileName = $"{Guid.NewGuid()}.webp";
        var objectKey = await storage.UploadContentImageAsync(processed.ProcessedImage, "lessons/thumbnails", fileName, ct);

        lesson.ThumbnailUrl = objectKey;
        lesson.UpdatedAt = dateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await cache.RemoveAsync($"avtolider:video-lesson:{request.LessonId}", ct);
        await CreateVideoCategoryCommandHandler.InvalidateVideoCategoryCacheAsync(cache, ct);

        if (!string.IsNullOrEmpty(oldThumbnailKey))
            await storage.DeleteAsync(oldThumbnailKey, ct);

        var presignedUrl = await storage.GetPresignedUrlAsync(objectKey, ct);

        logger.LogInformation("Thumbnail uploaded for lesson {LessonId}: {Key}", request.LessonId, objectKey);
        return ApiResponse<string>.Ok(presignedUrl);
    }
}
