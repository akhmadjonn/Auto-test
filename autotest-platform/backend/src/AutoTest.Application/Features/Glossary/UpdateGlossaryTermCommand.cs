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
    string TermUz,
    string TermUzLatin,
    string TermRu,
    string DefinitionUz,
    string DefinitionUzLatin,
    string DefinitionRu,
    int SortOrder,
    Guid[]? RelatedQuestionIds) : IRequest<ApiResponse>;

public class UpdateGlossaryTermCommandValidator : AbstractValidator<UpdateGlossaryTermCommand>
{
    public UpdateGlossaryTermCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
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
        term.Term = new LocalizedText(request.TermUz, request.TermUzLatin, request.TermRu);
        term.Definition = new LocalizedText(request.DefinitionUz, request.DefinitionUzLatin, request.DefinitionRu);
        term.SortOrder = request.SortOrder;
        term.RelatedQuestionIds = request.RelatedQuestionIds ?? [];
        term.UpdatedAt = dateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        await GetGlossaryCategoriesQueryHandler.InvalidateGlossaryCategoryCacheAsync(cache, ct);
        logger.LogInformation("Glossary term updated: {TermId}", request.Id);

        return ApiResponse.Ok();
    }
}
