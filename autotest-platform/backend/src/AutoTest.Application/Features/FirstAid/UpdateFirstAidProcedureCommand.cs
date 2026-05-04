using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.FirstAid;

// Id is set from the route in the controller — frontend doesn't put it in the body.
public record UpdateFirstAidProcedureCommand(
    Guid Id,
    string Slug,
    LocalizedText Name,
    LocalizedText? Summary,
    int SortOrder) : IRequest<ApiResponse>;

public class UpdateFirstAidProcedureCommandValidator : AbstractValidator<UpdateFirstAidProcedureCommand>
{
    public UpdateFirstAidProcedureCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Slug).NotEmpty().Matches("^[a-z0-9-]+$").WithMessage("Slug must contain only lowercase letters, digits, and hyphens.");
        RuleFor(x => x.Name).NotNull();
        RuleFor(x => x.Name.Uz).NotEmpty().When(x => x.Name is not null);
        RuleFor(x => x.Name.UzLatin).NotEmpty().When(x => x.Name is not null);
        RuleFor(x => x.Name.Ru).NotEmpty().When(x => x.Name is not null);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public class UpdateFirstAidProcedureCommandHandler(
    IApplicationDbContext db,
    IDateTimeProvider dateTime,
    ICacheService cache,
    ILogger<UpdateFirstAidProcedureCommandHandler> logger) : IRequestHandler<UpdateFirstAidProcedureCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(UpdateFirstAidProcedureCommand request, CancellationToken ct)
    {
        var procedure = await db.FirstAidProcedures.FindAsync([request.Id], ct);
        if (procedure is null)
            return ApiResponse.Fail("PROCEDURE_NOT_FOUND", "First aid procedure not found.");

        var slugExists = await db.FirstAidProcedures.AnyAsync(p => p.Slug == request.Slug && p.Id != request.Id, ct);
        if (slugExists)
            return ApiResponse.Fail("SLUG_ALREADY_EXISTS", $"A procedure with slug '{request.Slug}' already exists.");

        var oldSlug = procedure.Slug;

        procedure.Slug = request.Slug;
        procedure.Name = request.Name;
        procedure.Summary = request.Summary;
        procedure.SortOrder = request.SortOrder;
        procedure.UpdatedAt = dateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        await CreateFirstAidProcedureCommandHandler.InvalidateFirstAidCacheAsync(cache, oldSlug, ct);
        if (oldSlug != request.Slug)
            await cache.RemoveAsync($"avtolider:first-aid:{request.Slug}", ct);

        logger.LogInformation("Updated first aid procedure {ProcedureId}", request.Id);
        return ApiResponse.Ok();
    }
}
