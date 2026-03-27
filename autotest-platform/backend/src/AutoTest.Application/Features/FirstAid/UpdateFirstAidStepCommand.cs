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
    string TitleUz,
    string TitleUzLatin,
    string TitleRu,
    string DescriptionUz,
    string DescriptionUzLatin,
    string DescriptionRu) : IRequest<ApiResponse>;

public class UpdateFirstAidStepCommandValidator : AbstractValidator<UpdateFirstAidStepCommand>
{
    public UpdateFirstAidStepCommandValidator()
    {
        RuleFor(x => x.ProcedureId).NotEmpty();
        RuleFor(x => x.StepId).NotEmpty();
        RuleFor(x => x.StepOrder).GreaterThan(0);
        RuleFor(x => x.TitleUz).NotEmpty();
        RuleFor(x => x.TitleUzLatin).NotEmpty();
        RuleFor(x => x.TitleRu).NotEmpty();
        RuleFor(x => x.DescriptionUz).NotEmpty();
        RuleFor(x => x.DescriptionUzLatin).NotEmpty();
        RuleFor(x => x.DescriptionRu).NotEmpty();
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
        step.Title = new LocalizedText(request.TitleUz, request.TitleUzLatin, request.TitleRu);
        step.Description = new LocalizedText(request.DescriptionUz, request.DescriptionUzLatin, request.DescriptionRu);
        step.UpdatedAt = dateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        await CreateFirstAidProcedureCommandHandler.InvalidateFirstAidCacheAsync(cache, step.Procedure.Slug, ct);

        logger.LogInformation("Updated first aid step {StepId} in procedure {ProcedureId}", request.StepId, request.ProcedureId);
        return ApiResponse.Ok();
    }
}
