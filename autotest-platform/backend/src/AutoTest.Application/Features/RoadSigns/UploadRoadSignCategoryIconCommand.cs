using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.RoadSigns;

public record UploadRoadSignCategoryIconCommand(Guid Id, Stream Image, string FileName) : IRequest<ApiResponse<string>>;

public class UploadRoadSignCategoryIconCommandValidator : AbstractValidator<UploadRoadSignCategoryIconCommand>
{
    public UploadRoadSignCategoryIconCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Image).NotNull();
        RuleFor(x => x.FileName).NotEmpty();
    }
}

public class UploadRoadSignCategoryIconCommandHandler(
    IApplicationDbContext db,
    IImageProcessingService imageProcessor,
    IFileStorageService storage,
    ICacheService cache,
    IDateTimeProvider dateTime,
    ILogger<UploadRoadSignCategoryIconCommandHandler> logger) : IRequestHandler<UploadRoadSignCategoryIconCommand, ApiResponse<string>>
{
    public async Task<ApiResponse<string>> Handle(UploadRoadSignCategoryIconCommand request, CancellationToken ct)
    {
        var category = await db.RoadSignCategories.FindAsync([request.Id], ct);
        if (category is null)
            return ApiResponse<string>.Fail("NOT_FOUND", "Road sign category not found.");

        var oldIconKey = category.IconUrl;

        var processed = await imageProcessor.ProcessImageAsync(request.Image, request.FileName, ct);
        var fileName = $"{Guid.NewGuid()}.webp";
        var objectKey = await storage.UploadContentImageAsync(processed.ProcessedImage, "road-sign-categories", fileName, ct);

        category.IconUrl = objectKey;
        category.UpdatedAt = dateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await CreateRoadSignCategoryCommandHandler.InvalidateRoadSignCacheAsync(cache, ct);

        if (!string.IsNullOrEmpty(oldIconKey))
            await storage.DeleteAsync(oldIconKey, ct);

        var presignedUrl = await storage.GetPresignedUrlAsync(objectKey, ct);

        logger.LogInformation("Road sign category icon uploaded: {Id} key={Key}", request.Id, objectKey);
        return ApiResponse<string>.Ok(presignedUrl);
    }
}
