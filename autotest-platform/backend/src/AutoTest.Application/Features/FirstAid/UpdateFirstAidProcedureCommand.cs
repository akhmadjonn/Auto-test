using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.FirstAid;

public record UpdateFirstAidProcedureCommand(
    Guid Id,
    string Slug,
    string NameUz,
    string NameUzLatin,
    string NameRu,
    string? SummaryUz,
    string? SummaryUzLatin,
    string? SummaryRu,
    int SortOrder) : IRequest<ApiResponse>;

public class UpdateFirstAidProcedureCommandValidator : AbstractValidator<UpdateFirstAidProcedureCommand>
{
    public UpdateFirstAidProcedureCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Slug).NotEmpty().Matches("^[a-z0-9-]+$").WithMessage("Slug must contain only lowercase letters, digits, and hyphens.");
        RuleFor(x => x.NameUz).NotEmpty();
        RuleFor(x => x.NameUzLatin).NotEmpty();
        RuleFor(x => x.NameRu).NotEmpty();
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
        procedure.Name = new LocalizedText(request.NameUz, request.NameUzLatin, request.NameRu);
        procedure.Summary = request.SummaryUz is not null && request.SummaryUzLatin is not null && request.SummaryRu is not null
            ? new LocalizedText(request.SummaryUz, request.SummaryUzLatin, request.SummaryRu)
            : null;
        procedure.SortOrder = request.SortOrder;
        procedure.UpdatedAt = dateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        // Invalidate both old and new slug cache keys if slug changed
        await CreateFirstAidProcedureCommandHandler.InvalidateFirstAidCacheAsync(cache, oldSlug, ct);
        if (oldSlug != request.Slug)
            await cache.RemoveAsync($"avtolider:first-aid:{request.Slug}", ct);

        logger.LogInformation("Updated first aid procedure {ProcedureId}", request.Id);
        return ApiResponse.Ok();
    }
}
