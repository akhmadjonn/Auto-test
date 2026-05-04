using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.FirstAid;

public record UpdateFirstAidStepCommand(
    Guid ProcedureId,
    Guid StepId,
    int StepOrder,
    LocalizedText Title,
    LocalizedText Description) : IRequest<ApiResponse>;

public class UpdateFirstAidStepCommandValidator : AbstractValidator<UpdateFirstAidStepCommand>
{
    public UpdateFirstAidStepCommandValidator()
    {
        RuleFor(x => x.ProcedureId).NotEmpty();
        RuleFor(x => x.StepId).NotEmpty();
        RuleFor(x => x.StepOrder).GreaterThan(0);
        RuleFor(x => x.Title).NotNull();
        RuleFor(x => x.Title.Uz).NotEmpty().When(x => x.Title is not null);
        RuleFor(x => x.Title.UzLatin).NotEmpty().When(x => x.Title is not null);
        RuleFor(x => x.Title.Ru).NotEmpty().When(x => x.Title is not null);
        RuleFor(x => x.Description).NotNull();
        RuleFor(x => x.Description.Uz).NotEmpty().When(x => x.Description is not null);
        RuleFor(x => x.Description.UzLatin).NotEmpty().When(x => x.Description is not null);
        RuleFor(x => x.Description.Ru).NotEmpty().When(x => x.Description is not null);
    }
}

public class UpdateFirstAidStepCommandHandler(
    IApplicationDbContext db,
    IDateTimeProvider dateTime,
    ICacheService cache,
    ILogger<UpdateFirstAidStepCommandHandler> logger) : IRequestHandler<UpdateFirstAidStepCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(UpdateFirstAidStepCommand request, CancellationToken ct)
    {
        var step = await db.FirstAidSteps
            .Include(s => s.Procedure)
            .FirstOrDefaultAsync(s => s.Id == request.StepId && s.FirstAidProcedureId == request.ProcedureId, ct);

        if (step is null)
            return ApiResponse.Fail("STEP_NOT_FOUND", "First aid step not found or does not belong to the specified procedure.");

        step.StepOrder = request.StepOrder;
        step.Title = request.Title;
        step.Description = request.Description;
        step.UpdatedAt = dateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        await CreateFirstAidProcedureCommandHandler.InvalidateFirstAidCacheAsync(cache, step.Procedure.Slug, ct);

        logger.LogInformation("Updated first aid step {StepId} in procedure {ProcedureId}", request.StepId, request.ProcedureId);
        return ApiResponse.Ok();
    }
}
