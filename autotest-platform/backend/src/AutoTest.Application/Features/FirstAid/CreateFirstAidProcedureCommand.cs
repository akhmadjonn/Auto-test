using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.ValueObjects;
using AutoTest.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.FirstAid;

public record CreateFirstAidProcedureCommand(
    string Slug,
    string NameUz,
    string NameUzLatin,
    string NameRu,
    string? SummaryUz,
    string? SummaryUzLatin,
    string? SummaryRu,
    int SortOrder) : IRequest<ApiResponse<Guid>>;

public class CreateFirstAidProcedureCommandValidator : AbstractValidator<CreateFirstAidProcedureCommand>
{
    public CreateFirstAidProcedureCommandValidator()
    {
        RuleFor(x => x.Slug).NotEmpty().Matches("^[a-z0-9-]+$").WithMessage("Slug must contain only lowercase letters, digits, and hyphens.");
        RuleFor(x => x.NameUz).NotEmpty();
        RuleFor(x => x.NameUzLatin).NotEmpty();
        RuleFor(x => x.NameRu).NotEmpty();
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

        var summary = request.SummaryUz is not null && request.SummaryUzLatin is not null && request.SummaryRu is not null
            ? new LocalizedText(request.SummaryUz, request.SummaryUzLatin, request.SummaryRu)
            : null;

        var procedure = new FirstAidProcedure
        {
            Id = Guid.NewGuid(),
            Slug = request.Slug,
            Name = new LocalizedText(request.NameUz, request.NameUzLatin, request.NameRu),
            Summary = summary,
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
