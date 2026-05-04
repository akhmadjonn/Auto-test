using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Glossary;

public record UpdateGlossaryTermCommand(
    Guid Id,
    Guid GlossaryCategoryId,
    LocalizedText Term,
    LocalizedText Definition,
    int SortOrder,
    Guid[]? RelatedQuestionIds) : IRequest<ApiResponse>;

public class UpdateGlossaryTermCommandValidator : AbstractValidator<UpdateGlossaryTermCommand>
{
    public UpdateGlossaryTermCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.GlossaryCategoryId).NotEmpty();
        RuleFor(x => x.Term).NotNull();
        RuleFor(x => x.Term.Uz).NotEmpty().MaximumLength(500).When(x => x.Term is not null);
        RuleFor(x => x.Term.UzLatin).NotEmpty().MaximumLength(500).When(x => x.Term is not null);
        RuleFor(x => x.Term.Ru).NotEmpty().MaximumLength(500).When(x => x.Term is not null);
        RuleFor(x => x.Definition).NotNull();
        RuleFor(x => x.Definition.Uz).NotEmpty().MaximumLength(5000).When(x => x.Definition is not null);
        RuleFor(x => x.Definition.UzLatin).NotEmpty().MaximumLength(5000).When(x => x.Definition is not null);
        RuleFor(x => x.Definition.Ru).NotEmpty().MaximumLength(5000).When(x => x.Definition is not null);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public class UpdateGlossaryTermCommandHandler(
    IApplicationDbContext db,
    ICacheService cache,
    IDateTimeProvider dateTime,
    ILogger<UpdateGlossaryTermCommandHandler> logger) : IRequestHandler<UpdateGlossaryTermCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(UpdateGlossaryTermCommand request, CancellationToken ct)
    {
        var term = await db.GlossaryTerms.FirstOrDefaultAsync(t => t.Id == request.Id, ct);
        if (term is null)
            return ApiResponse.Fail("TERM_NOT_FOUND", "Glossary term not found.");

        var categoryExists = await db.GlossaryCategories.AnyAsync(c => c.Id == request.GlossaryCategoryId, ct);
        if (!categoryExists)
            return ApiResponse.Fail("CATEGORY_NOT_FOUND", "Glossary category not found.");

        term.GlossaryCategoryId = request.GlossaryCategoryId;
        term.Term = request.Term;
        term.Definition = request.Definition;
        term.SortOrder = request.SortOrder;
        term.RelatedQuestionIds = request.RelatedQuestionIds ?? [];
        term.UpdatedAt = dateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        await GetGlossaryCategoriesQueryHandler.InvalidateGlossaryCategoryCacheAsync(cache, ct);
        logger.LogInformation("Glossary term updated: {TermId}", request.Id);

        return ApiResponse.Ok();
    }
}
