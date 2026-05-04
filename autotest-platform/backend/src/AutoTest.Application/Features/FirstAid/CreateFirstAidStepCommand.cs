using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.ValueObjects;
using AutoTest.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.FirstAid;

public record CreateFirstAidStepCommand(
    Guid ProcedureId,
    int StepOrder,
    LocalizedText Title,
    LocalizedText Description) : IRequest<ApiResponse<Guid>>;

public class CreateFirstAidStepCommandValidator : AbstractValidator<CreateFirstAidStepCommand>
{
    public CreateFirstAidStepCommandValidator()
    {
        RuleFor(x => x.ProcedureId).NotEmpty();
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

public class CreateFirstAidStepCommandHandler(
    IApplicationDbContext db,
    IDateTimeProvider dateTime,
    ICacheService cache,
    ILogger<CreateFirstAidStepCommandHandler> logger) : IRequestHandler<CreateFirstAidStepCommand, ApiResponse<Guid>>
{
    public async Task<ApiResponse<Guid>> Handle(CreateFirstAidStepCommand request, CancellationToken ct)
    {
        var procedure = await db.FirstAidProcedures
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProcedureId, ct);

        if (procedure is null)
            return ApiResponse<Guid>.Fail("PROCEDURE_NOT_FOUND", "First aid procedure not found.");

        var step = new FirstAidStep
        {
            Id = Guid.NewGuid(),
            FirstAidProcedureId = request.ProcedureId,
            StepOrder = request.StepOrder,
            Title = request.Title,
            Description = request.Description,
            CreatedAt = dateTime.UtcNow,
            UpdatedAt = dateTime.UtcNow
        };

        db.FirstAidSteps.Add(step);
        await db.SaveChangesAsync(ct);
        await CreateFirstAidProcedureCommandHandler.InvalidateFirstAidCacheAsync(cache, procedure.Slug, ct);

        logger.LogInformation("Created first aid step {StepId} for procedure {ProcedureId}", step.Id, request.ProcedureId);
        return ApiResponse<Guid>.Ok(step.Id);
    }
}
