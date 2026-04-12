using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.VideoLessons;

public record UploadVideoFileCommand(Guid LessonId, Stream FileStream, string FileName, string ContentType) : IRequest<ApiResponse<string>>;

public class UploadVideoFileCommandValidator : AbstractValidator<UploadVideoFileCommand>
{
    public UploadVideoFileCommandValidator()
    {
        RuleFor(x => x.LessonId).NotEmpty();
        RuleFor(x => x.FileStream).NotNull();
        RuleFor(x => x.FileName).NotEmpty();
    }
}

public class UploadVideoFileCommandHandler(
    IApplicationDbContext db,
    IFileStorageService storage,
    IDateTimeProvider dateTime,
    ICacheService cache,
    ILogger<UploadVideoFileCommandHandler> logger) : IRequestHandler<UploadVideoFileCommand, ApiResponse<string>>
{
    public async Task<ApiResponse<string>> Handle(UploadVideoFileCommand request, CancellationToken ct)
    {
        var lesson = await db.VideoLessons.FindAsync([request.LessonId], ct);
        if (lesson is null)
            return ApiResponse<string>.Fail("NOT_FOUND", "Video lesson not found.");

        var oldVideoKey = lesson.VideoUrl;
        var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension))
            extension = ".mp4";

        var fileName = $"{Guid.NewGuid()}{extension}";
        var objectKey = await storage.UploadFileAsync(request.FileStream, "lessons/videos", fileName, request.ContentType, ct);

        lesson.VideoUrl = objectKey;
        lesson.SourceType = Domain.Common.Enums.VideoSourceType.Upload;
        lesson.UpdatedAt = dateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await cache.RemoveAsync($"avtolider:video-lesson:{request.LessonId}", ct);
        await CreateVideoCategoryCommandHandler.InvalidateVideoCategoryCacheAsync(cache, ct);

        // delete old uploaded video if it was a MinIO path
        if (!string.IsNullOrEmpty(oldVideoKey) && oldVideoKey.StartsWith("lessons/"))
            await storage.DeleteAsync(oldVideoKey, ct);

        var presignedUrl = await storage.GetPresignedUrlAsync(objectKey, ct);

        logger.LogInformation("Video file uploaded for lesson {LessonId}: {Key}", request.LessonId, objectKey);
        return ApiResponse<string>.Ok(presignedUrl);
    }
}
