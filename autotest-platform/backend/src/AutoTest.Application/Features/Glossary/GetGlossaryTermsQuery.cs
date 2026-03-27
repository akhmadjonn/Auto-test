using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.Glossary;

public record GetGlossaryTermsQuery(string CategorySlug, int Page = 1, int PageSize = 50) : IRequest<ApiResponse<PaginatedList<GlossaryTermDto>>>;

public record GlossaryTermDto(
    Guid Id,
    LocalizedText Term,
    LocalizedText Definition,
    int SortOrder,
    Guid[] RelatedQuestionIds);

public class GetGlossaryTermsQueryValidator : AbstractValidator<GetGlossaryTermsQuery>
{
    public GetGlossaryTermsQueryValidator()
    {
        RuleFor(x => x.CategorySlug).NotEmpty();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public class GetGlossaryTermsQueryHandler(
    IApplicationDbContext db) : IRequestHandler<GetGlossaryTermsQuery, ApiResponse<PaginatedList<GlossaryTermDto>>>
{
    public async Task<ApiResponse<PaginatedList<GlossaryTermDto>>> Handle(GetGlossaryTermsQuery request, CancellationToken ct)
    {
        var category = await db.GlossaryCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Slug == request.CategorySlug, ct);

        if (category is null)
            return ApiResponse<PaginatedList<GlossaryTermDto>>.Fail("CATEGORY_NOT_FOUND", $"Glossary category with slug '{request.CategorySlug}' not found.");

        var query = db.GlossaryTerms
            .AsNoTracking()
            .Where(t => t.GlossaryCategoryId == category.Id)
            .OrderBy(t => t.SortOrder)
            .Select(t => new GlossaryTermDto(
                t.Id,
                t.Term,
                t.Definition,
                t.SortOrder,
                t.RelatedQuestionIds));

        var result = await PaginatedList<GlossaryTermDto>.CreateAsync(query, request.Page, request.PageSize, ct);
        return ApiResponse<PaginatedList<GlossaryTermDto>>.Ok(result);
    }
}
