using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.ValueObjects;
using AutoTest.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Glossary;

public record CreateGlossaryTermCommand(
    Guid GlossaryCategoryId,
    LocalizedText Term,
    LocalizedText Definition,
    int SortOrder,
    Guid[]? RelatedQuestionIds) : IRequest<ApiResponse<Guid>>;

public class CreateGlossaryTermCommandValidator : AbstractValidator<CreateGlossaryTermCommand>
{
    public CreateGlossaryTermCommandValidator()
    {
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

public class CreateGlossaryTermCommandHandler(
    IApplicationDbContext db,
    ICacheService cache,
    IDateTimeProvider dateTime,
    ILogger<CreateGlossaryTermCommandHandler> logger) : IRequestHandler<CreateGlossaryTermCommand, ApiResponse<Guid>>
{
    public async Task<ApiResponse<Guid>> Handle(CreateGlossaryTermCommand request, CancellationToken ct)
    {
        var categoryExists = await db.GlossaryCategories.AnyAsync(c => c.Id == request.GlossaryCategoryId, ct);
        if (!categoryExists)
            return ApiResponse<Guid>.Fail("CATEGORY_NOT_FOUND", "Glossary category not found.");

        var term = new GlossaryTerm
        {
            Id = Guid.NewGuid(),
            GlossaryCategoryId = request.GlossaryCategoryId,
            Term = request.Term,
            Definition = request.Definition,
            SortOrder = request.SortOrder,
            RelatedQuestionIds = request.RelatedQuestionIds ?? [],
            CreatedAt = dateTime.UtcNow
        };

        db.GlossaryTerms.Add(term);
        await db.SaveChangesAsync(ct);

        await GetGlossaryCategoriesQueryHandler.InvalidateGlossaryCategoryCacheAsync(cache, ct);
        logger.LogInformation("Glossary term created: {TermId} in category {CategoryId}", term.Id, request.GlossaryCategoryId);

        return ApiResponse<Guid>.Ok(term.Id);
    }
}
