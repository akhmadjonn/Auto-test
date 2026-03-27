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
    string TermUz,
    string TermUzLatin,
    string TermRu,
    string DefinitionUz,
    string DefinitionUzLatin,
    string DefinitionRu,
    int SortOrder,
    Guid[]? RelatedQuestionIds) : IRequest<ApiResponse<Guid>>;

public class CreateGlossaryTermCommandValidator : AbstractValidator<CreateGlossaryTermCommand>
{
    public CreateGlossaryTermCommandValidator()
    {
        RuleFor(x => x.GlossaryCategoryId).NotEmpty();
        RuleFor(x => x.TermUz).NotEmpty().MaximumLength(500);
        RuleFor(x => x.TermUzLatin).NotEmpty().MaximumLength(500);
        RuleFor(x => x.TermRu).NotEmpty().MaximumLength(500);
        RuleFor(x => x.DefinitionUz).NotEmpty().MaximumLength(5000);
        RuleFor(x => x.DefinitionUzLatin).NotEmpty().MaximumLength(5000);
        RuleFor(x => x.DefinitionRu).NotEmpty().MaximumLength(5000);
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
            Term = new LocalizedText(request.TermUz, request.TermUzLatin, request.TermRu),
            Definition = new LocalizedText(request.DefinitionUz, request.DefinitionUzLatin, request.DefinitionRu),
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
