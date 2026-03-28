using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.ColorVision;

public record UploadColorVisionPlateImageCommand(Guid Id, Stream Image, string FileName) : IRequest<ApiResponse<string>>;

public class UploadColorVisionPlateImageCommandValidator : AbstractValidator<UploadColorVisionPlateImageCommand>
{
    public UploadColorVisionPlateImageCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Image).NotNull();
        RuleFor(x => x.FileName).NotEmpty();
    }
}

public class UploadColorVisionPlateImageCommandHandler(
    IApplicationDbContext db,
    IImageProcessingService imageProcessor,
    IFileStorageService storage,
    ICacheService cache,
    IDateTimeProvider dateTime,
    ILogger<UploadColorVisionPlateImageCommandHandler> logger) : IRequestHandler<UploadColorVisionPlateImageCommand, ApiResponse<string>>
{
    public async Task<ApiResponse<string>> Handle(UploadColorVisionPlateImageCommand request, CancellationToken ct)
    {
        var plate = await db.ColorVisionPlates.FindAsync([request.Id], ct);
        if (plate is null)
            return ApiResponse<string>.Fail("NOT_FOUND", "Color vision plate not found.");

        var oldImageKey = plate.ImageUrl;

        var processed = await imageProcessor.ProcessImageAsync(request.Image, request.FileName, ct);
        var fileName = $"{Guid.NewGuid()}.webp";
        var objectKey = await storage.UploadContentImageAsync(processed.ProcessedImage, "color-vision", fileName, ct);

        plate.ImageUrl = objectKey;
        plate.UpdatedAt = dateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await CreateColorVisionPlateCommandHandler.InvalidateColorVisionCacheAsync(cache, ct);

        if (!string.IsNullOrEmpty(oldImageKey))
            await storage.DeleteAsync(oldImageKey, ct);

        var presignedUrl = await storage.GetPresignedUrlAsync(objectKey, ct);

        logger.LogInformation("Color vision plate image uploaded: {Id} key={Key}", request.Id, objectKey);
        return ApiResponse<string>.Ok(presignedUrl);
    }
}
