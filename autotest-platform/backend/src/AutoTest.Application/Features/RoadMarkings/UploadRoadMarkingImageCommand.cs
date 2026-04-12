using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.RoadMarkings;

public record UploadRoadMarkingImageCommand(Guid Id, Stream Image, string FileName) : IRequest<ApiResponse<string>>;

public class UploadRoadMarkingImageCommandValidator : AbstractValidator<UploadRoadMarkingImageCommand>
{
    public UploadRoadMarkingImageCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Image).NotNull();
        RuleFor(x => x.FileName).NotEmpty();
    }
}

public class UploadRoadMarkingImageCommandHandler(
    IApplicationDbContext db,
    IImageProcessingService imageProcessor,
    IFileStorageService storage,
    ICacheService cache,
    IDateTimeProvider dateTime,
    ILogger<UploadRoadMarkingImageCommandHandler> logger) : IRequestHandler<UploadRoadMarkingImageCommand, ApiResponse<string>>
{
    public async Task<ApiResponse<string>> Handle(UploadRoadMarkingImageCommand request, CancellationToken ct)
    {
        var marking = await db.RoadMarkings.FindAsync([request.Id], ct);
        if (marking is null)
            return ApiResponse<string>.Fail("NOT_FOUND", "Road marking not found.");

        var oldImageKey = marking.ImageUrl;
        var oldThumbKey = marking.ThumbnailUrl;

        var processed = await imageProcessor.ProcessImageAsync(request.Image, request.FileName, ct);
        var fileName = $"{Guid.NewGuid()}.webp";
        var thumbFileName = $"{Guid.NewGuid()}_thumb.webp";

        var objectKey = await storage.UploadContentImageAsync(processed.ProcessedImage, "road-markings", fileName, ct);
        var thumbKey = await storage.UploadContentImageAsync(processed.Thumbnail, "road-markings", thumbFileName, ct);

        marking.ImageUrl = objectKey;
        marking.ThumbnailUrl = thumbKey;
        marking.UpdatedAt = dateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await CreateRoadMarkingCommandHandler.InvalidateRoadMarkingCacheAsync(cache, ct);
        await cache.RemoveAsync($"avtolider:road-markings:{request.Id}", ct);

        if (!string.IsNullOrEmpty(oldImageKey))
            await storage.DeleteAsync(oldImageKey, ct);
        if (!string.IsNullOrEmpty(oldThumbKey) && oldThumbKey != oldImageKey)
            await storage.DeleteAsync(oldThumbKey, ct);

        var presignedUrl = await storage.GetPresignedUrlAsync(objectKey, ct);

        logger.LogInformation("Road marking image uploaded: {Id} key={Key}", request.Id, objectKey);
        return ApiResponse<string>.Ok(presignedUrl);
    }
}
