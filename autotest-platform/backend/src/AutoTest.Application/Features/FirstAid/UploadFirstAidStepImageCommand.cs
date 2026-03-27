using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.FirstAid;

public record UploadFirstAidStepImageCommand(
    Guid ProcedureId,
    Guid StepId,
    Stream Image,
    string FileName) : IRequest<ApiResponse>;

public class UploadFirstAidStepImageCommandValidator : AbstractValidator<UploadFirstAidStepImageCommand>
{
    public UploadFirstAidStepImageCommandValidator()
    {
        RuleFor(x => x.ProcedureId).NotEmpty();
        RuleFor(x => x.StepId).NotEmpty();
        RuleFor(x => x.Image).NotNull();
        RuleFor(x => x.FileName).NotEmpty();
    }
}

public class UploadFirstAidStepImageCommandHandler(
    IApplicationDbContext db,
    IImageProcessingService imageProcessor,
    IFileStorageService storage,
    IDateTimeProvider dateTime,
    ICacheService cache,
    ILogger<UploadFirstAidStepImageCommandHandler> logger) : IRequestHandler<UploadFirstAidStepImageCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(UploadFirstAidStepImageCommand request, CancellationToken ct)
    {
        var step = await db.FirstAidSteps
            .Include(s => s.Procedure)
            .FirstOrDefaultAsync(s => s.Id == request.StepId && s.FirstAidProcedureId == request.ProcedureId, ct);

        if (step is null)
            return ApiResponse.Fail("STEP_NOT_FOUND", "First aid step not found or does not belong to the specified procedure.");

        // Delete old image if exists
        if (step.ImageUrl is not null)
            await storage.DeleteAsync(step.ImageUrl, ct);

        var processed = await imageProcessor.ProcessImageAsync(request.Image, request.FileName, ct);
        var guid = Guid.NewGuid().ToString();
        var objectKey = await storage.UploadContentImageAsync(processed.ProcessedImage, "first-aid", $"{guid}.webp", ct);

        step.ImageUrl = objectKey;
        step.UpdatedAt = dateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        await CreateFirstAidProcedureCommandHandler.InvalidateFirstAidCacheAsync(cache, step.Procedure.Slug, ct);

        logger.LogInformation("Uploaded image for first aid step {StepId}", request.StepId);
        return ApiResponse.Ok();
    }
}
