using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.TrafficFines;

public record UploadFineImageCommand(Guid Id, Stream Image, string FileName) : IRequest<ApiResponse<string>>;

public class UploadFineImageCommandValidator : AbstractValidator<UploadFineImageCommand>
{
    public UploadFineImageCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Image).NotNull();
        RuleFor(x => x.FileName).NotEmpty();
    }
}

public class UploadFineImageCommandHandler(
    IApplicationDbContext db,
    IImageProcessingService imageProcessor,
    IFileStorageService storage,
    IDateTimeProvider dateTime,
    ICacheService cache,
    ILogger<UploadFineImageCommandHandler> logger) : IRequestHandler<UploadFineImageCommand, ApiResponse<string>>
{
    public async Task<ApiResponse<string>> Handle(UploadFineImageCommand request, CancellationToken ct)
    {
        var fine = await db.TrafficFines.FindAsync([request.Id], ct);
        if (fine is null)
            return ApiResponse<string>.Fail("NOT_FOUND", "Traffic fine not found.");

        var processed = await imageProcessor.ProcessImageAsync(request.Image, request.FileName, ct);
        var guid = Guid.NewGuid().ToString();
        var objectKey = await storage.UploadContentImageAsync(processed.ProcessedImage, "fines", $"{guid}.webp", ct);

        // delete old image if exists
        if (fine.ImageUrl is not null)
            await storage.DeleteAsync(fine.ImageUrl, ct);

        fine.ImageUrl = objectKey;
        fine.UpdatedAt = dateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        await CreateFineCommandHandler.InvalidateFineCachesAsync(cache, request.Id, ct);

        var presignedUrl = await storage.GetPresignedUrlAsync(objectKey, ct);

        logger.LogInformation("Uploaded image for traffic fine {FineId}", request.Id);
        return ApiResponse<string>.Ok(presignedUrl);
    }
}
