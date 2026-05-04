using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.ValueObjects;
using AutoTest.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.FirstAid;

// Accepts nested LocalizedText to match the JSON shape the frontend sends
// (`{ "name": { "uz": ..., "uzLatin": ..., "ru": ... } }`). The previous
// flat NameUz / NameUzLatin / NameRu binding silently failed because JSON
// was nested → all three flat fields stayed empty → validation rejected it.
public record CreateFirstAidProcedureCommand(
    string Slug,
    LocalizedText Name,
    LocalizedText? Summary,
    int SortOrder) : IRequest<ApiResponse<Guid>>;

public class CreateFirstAidProcedureCommandValidator : AbstractValidator<CreateFirstAidProcedureCommand>
{
    public CreateFirstAidProcedureCommandValidator()
    {
        RuleFor(x => x.Slug).NotEmpty().Matches("^[a-z0-9-]+$").WithMessage("Slug must contain only lowercase letters, digits, and hyphens.");
        RuleFor(x => x.Name).NotNull();
        RuleFor(x => x.Name.Uz).NotEmpty().When(x => x.Name is not null);
        RuleFor(x => x.Name.UzLatin).NotEmpty().When(x => x.Name is not null);
        RuleFor(x => x.Name.Ru).NotEmpty().When(x => x.Name is not null);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public class CreateFirstAidProcedureCommandHandler(
    IApplicationDbContext db,
    IDateTimeProvider dateTime,
    ICacheService cache,
    ILogger<CreateFirstAidProcedureCommandHandler> logger) : IRequestHandler<CreateFirstAidProcedureCommand, ApiResponse<Guid>>
{
    public async Task<ApiResponse<Guid>> Handle(CreateFirstAidProcedureCommand request, CancellationToken ct)
    {
        var slugExists = await db.FirstAidProcedures.AnyAsync(p => p.Slug == request.Slug, ct);
        if (slugExists)
            return ApiResponse<Guid>.Fail("SLUG_ALREADY_EXISTS", $"A procedure with slug '{request.Slug}' already exists.");

        var procedure = new FirstAidProcedure
        {
            Id = Guid.NewGuid(),
            Slug = request.Slug,
            Name = request.Name,
            Summary = request.Summary,
            SortOrder = request.SortOrder,
            CreatedAt = dateTime.UtcNow,
            UpdatedAt = dateTime.UtcNow
        };

        db.FirstAidProcedures.Add(procedure);
        await db.SaveChangesAsync(ct);
        await InvalidateFirstAidCacheAsync(cache, ct);

        logger.LogInformation("Created first aid procedure {ProcedureId} with slug {Slug}", procedure.Id, request.Slug);
        return ApiResponse<Guid>.Ok(procedure.Id);
    }

    public static async Task InvalidateFirstAidCacheAsync(ICacheService cache, CancellationToken ct)
    {
        await cache.RemoveAsync("avtolider:first-aid:all", ct);
    }

    public static async Task InvalidateFirstAidCacheAsync(ICacheService cache, string slug, CancellationToken ct)
    {
        await cache.RemoveAsync("avtolider:first-aid:all", ct);
        await cache.RemoveAsync($"avtolider:first-aid:{slug}", ct);
    }
}
