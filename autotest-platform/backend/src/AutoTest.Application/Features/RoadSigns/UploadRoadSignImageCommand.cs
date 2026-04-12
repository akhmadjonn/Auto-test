using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.RoadSigns;

public record UploadRoadSignImageCommand(Guid Id, Stream Image, string FileName) : IRequest<ApiResponse<string>>;

public class UploadRoadSignImageCommandValidator : AbstractValidator<UploadRoadSignImageCommand>
{
    public UploadRoadSignImageCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Image).NotNull();
        RuleFor(x => x.FileName).NotEmpty();
    }
}

public class UploadRoadSignImageCommandHandler(
    IApplicationDbContext db,
    IImageProcessingService imageProcessor,
    IFileStorageService storage,
    ICacheService cache,
    IDateTimeProvider dateTime,
    ILogger<UploadRoadSignImageCommandHandler> logger) : IRequestHandler<UploadRoadSignImageCommand, ApiResponse<string>>
{
    public async Task<ApiResponse<string>> Handle(UploadRoadSignImageCommand request, CancellationToken ct)
    {
        var sign = await db.RoadSigns.FindAsync([request.Id], ct);
        if (sign is null)
            return ApiResponse<string>.Fail("NOT_FOUND", "Road sign not found.");

        var oldImageKey = sign.ImageUrl;
        var oldThumbKey = sign.ThumbnailUrl;

        var processed = await imageProcessor.ProcessImageAsync(request.Image, request.FileName, ct);
        var fileName = $"{Guid.NewGuid()}.webp";
        var thumbFileName = $"{Guid.NewGuid()}_thumb.webp";

        var objectKey = await storage.UploadContentImageAsync(processed.ProcessedImage, "road-signs", fileName, ct);
        var thumbKey = await storage.UploadContentImageAsync(processed.Thumbnail, "road-signs", thumbFileName, ct);

        sign.ImageUrl = objectKey;
        sign.ThumbnailUrl = thumbKey;
        sign.UpdatedAt = dateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await CreateRoadSignCategoryCommandHandler.InvalidateRoadSignCacheAsync(cache, ct);
        await cache.RemoveAsync($"avtolider:road-signs:category:{sign.CategoryId}", ct);
        await cache.RemoveAsync($"avtolider:road-signs:{request.Id}", ct);

        if (!string.IsNullOrEmpty(oldImageKey))
            await storage.DeleteAsync(oldImageKey, ct);
        if (!string.IsNullOrEmpty(oldThumbKey) && oldThumbKey != oldImageKey)
            await storage.DeleteAsync(oldThumbKey, ct);

        var presignedUrl = await storage.GetPresignedUrlAsync(objectKey, ct);

        logger.LogInformation("Road sign image uploaded: {Id} key={Key}", request.Id, objectKey);
        return ApiResponse<string>.Ok(presignedUrl);
    }
}
