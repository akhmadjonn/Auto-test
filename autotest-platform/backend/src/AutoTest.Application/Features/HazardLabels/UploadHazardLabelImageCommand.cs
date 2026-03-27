using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.HazardLabels;

public record UploadHazardLabelImageCommand(Guid Id, Stream Image, string FileName) : IRequest<ApiResponse<string>>;

public class UploadHazardLabelImageCommandValidator : AbstractValidator<UploadHazardLabelImageCommand>
{
    public UploadHazardLabelImageCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Image).NotNull();
        RuleFor(x => x.FileName).NotEmpty();
    }
}

public class UploadHazardLabelImageCommandHandler(
    IApplicationDbContext db,
    IImageProcessingService imageProcessor,
    IFileStorageService storage,
    ICacheService cache,
    IDateTimeProvider dateTime,
    ILogger<UploadHazardLabelImageCommandHandler> logger) : IRequestHandler<UploadHazardLabelImageCommand, ApiResponse<string>>
{
    public async Task<ApiResponse<string>> Handle(UploadHazardLabelImageCommand request, CancellationToken ct)
    {
        var label = await db.HazardLabels.FindAsync([request.Id], ct);
        if (label is null)
            return ApiResponse<string>.Fail("NOT_FOUND", "Hazard label not found.");

        var oldImageKey = label.ImageUrl;

        var processed = await imageProcessor.ProcessImageAsync(request.Image, request.FileName, ct);
        var fileName = $"{Guid.NewGuid()}.webp";
        var objectKey = await storage.UploadContentImageAsync(processed.ProcessedImage, "hazard-labels", fileName, ct);

        label.ImageUrl = objectKey;
        label.UpdatedAt = dateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await CreateHazardLabelCommandHandler.InvalidateHazardLabelCacheAsync(cache, ct);
        await cache.RemoveAsync($"avtolider:hazard-labels:{request.Id}", ct);

        if (!string.IsNullOrEmpty(oldImageKey))
            await storage.DeleteAsync(oldImageKey, ct);

        var presignedUrl = await storage.GetPresignedUrlAsync(objectKey, ct);

        logger.LogInformation("Hazard label image uploaded: {Id} key={Key}", request.Id, objectKey);
        return ApiResponse<string>.Ok(presignedUrl);
    }
}
