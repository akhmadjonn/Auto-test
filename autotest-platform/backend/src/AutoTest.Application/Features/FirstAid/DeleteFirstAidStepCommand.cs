using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.FirstAid;

public record DeleteFirstAidStepCommand(Guid ProcedureId, Guid StepId) : IRequest<ApiResponse>;

public class DeleteFirstAidStepCommandValidator : AbstractValidator<DeleteFirstAidStepCommand>
{
    public DeleteFirstAidStepCommandValidator()
    {
        RuleFor(x => x.ProcedureId).NotEmpty();
        RuleFor(x => x.StepId).NotEmpty();
    }
}

public class DeleteFirstAidStepCommandHandler(
    IApplicationDbContext db,
    IFileStorageService storage,
    ICacheService cache,
    ILogger<DeleteFirstAidStepCommandHandler> logger) : IRequestHandler<DeleteFirstAidStepCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(DeleteFirstAidStepCommand request, CancellationToken ct)
    {
        var step = await db.FirstAidSteps
            .Include(s => s.Procedure)
            .FirstOrDefaultAsync(s => s.Id == request.StepId && s.FirstAidProcedureId == request.ProcedureId, ct);

        if (step is null)
            return ApiResponse.Fail("STEP_NOT_FOUND", "First aid step not found or does not belong to the specified procedure.");

        var imageKey = step.ImageUrl;
        var slug = step.Procedure.Slug;

        db.FirstAidSteps.Remove(step);
        await db.SaveChangesAsync(ct);
        await CreateFirstAidProcedureCommandHandler.InvalidateFirstAidCacheAsync(cache, slug, ct);

        if (imageKey is not null)
            await storage.DeleteAsync(imageKey, ct);

        logger.LogInformation("Deleted first aid step {StepId} from procedure {ProcedureId}", request.StepId, request.ProcedureId);
        return ApiResponse.Ok();
    }
}
