using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.FirstAid;

// Mirrors UploadHazardLabelImageCommand: lets admin upload a procedure icon
// directly from disk. Existing PUT /admin/first-aid/{id} still accepts a URL
// for the iconUrl field — both flows are valid.
public record UploadFirstAidProcedureIconCommand(
    Guid ProcedureId,
    Stream Image,
    string FileName) : IRequest<ApiResponse<string>>;

public class UploadFirstAidProcedureIconCommandValidator : AbstractValidator<UploadFirstAidProcedureIconCommand>
{
    public UploadFirstAidProcedureIconCommandValidator()
    {
        RuleFor(x => x.ProcedureId).NotEmpty();
        RuleFor(x => x.Image).NotNull();
        RuleFor(x => x.FileName).NotEmpty();
    }
}

public class UploadFirstAidProcedureIconCommandHandler(
    IApplicationDbContext db,
    IImageProcessingService imageProcessor,
    IFileStorageService storage,
    IDateTimeProvider dateTime,
    ICacheService cache,
    ILogger<UploadFirstAidProcedureIconCommandHandler> logger)
    : IRequestHandler<UploadFirstAidProcedureIconCommand, ApiResponse<string>>
{
    public async Task<ApiResponse<string>> Handle(UploadFirstAidProcedureIconCommand request, CancellationToken ct)
    {
        var procedure = await db.FirstAidProcedures.FindAsync([request.ProcedureId], ct);
        if (procedure is null)
            return ApiResponse<string>.Fail("PROCEDURE_NOT_FOUND", "First aid procedure not found.");

        var oldIconKey = procedure.IconUrl;

        var processed = await imageProcessor.ProcessImageAsync(request.Image, request.FileName, ct);
        var guid = Guid.NewGuid().ToString();
        var objectKey = await storage.UploadContentImageAsync(processed.ProcessedImage, "first-aid", $"{guid}.webp", ct);

        procedure.IconUrl = objectKey;
        procedure.UpdatedAt = dateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await CreateFirstAidProcedureCommandHandler.InvalidateFirstAidCacheAsync(cache, procedure.Slug, ct);

        // Best-effort cleanup of the previous file. Skip "visual/first_aid/..."
        // keys (those came from the migration tool's deterministic upload and
        // may be shared across reseeds).
        if (!string.IsNullOrEmpty(oldIconKey) && !oldIconKey.StartsWith("visual/first_aid/"))
            await storage.DeleteAsync(oldIconKey, ct);

        var presignedUrl = await storage.GetPresignedUrlAsync(objectKey, ct);

        logger.LogInformation("First aid procedure icon uploaded: {Id} key={Key}", request.ProcedureId, objectKey);
        return ApiResponse<string>.Ok(presignedUrl);
    }
}
