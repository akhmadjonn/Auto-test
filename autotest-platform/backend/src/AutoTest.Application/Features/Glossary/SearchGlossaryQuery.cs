using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.Glossary;

public record SearchGlossaryQuery(string Query, int Page = 1, int PageSize = 20) : IRequest<ApiResponse<PaginatedList<GlossaryTermDto>>>;

public class SearchGlossaryQueryValidator : AbstractValidator<SearchGlossaryQuery>
{
    public SearchGlossaryQueryValidator()
    {
        RuleFor(x => x.Query).NotEmpty().MinimumLength(2).MaximumLength(200);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public class SearchGlossaryQueryHandler(
    IApplicationDbContext db) : IRequestHandler<SearchGlossaryQuery, ApiResponse<PaginatedList<GlossaryTermDto>>>
{
    public async Task<ApiResponse<PaginatedList<GlossaryTermDto>>> Handle(SearchGlossaryQuery request, CancellationToken ct)
    {
        var search = request.Query.ToLower();

        var ordered = db.GlossaryTerms
            .AsNoTracking()
            .Where(t =>
                t.Term.Uz.ToLower().Contains(search) ||
                t.Term.UzLatin.ToLower().Contains(search) ||
                t.Term.Ru.ToLower().Contains(search) ||
                t.Definition.Uz.ToLower().Contains(search) ||
                t.Definition.UzLatin.ToLower().Contains(search) ||
                t.Definition.Ru.ToLower().Contains(search))
            .OrderBy(t => t.SortOrder);

        var totalCount = await ordered.CountAsync(ct);
        var entities = await ordered
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var items = entities.Select(t => new GlossaryTermDto(
            t.Id,
            t.Term,
            t.Definition,
            t.SortOrder,
            t.RelatedQuestionIds)).ToList();

        var result = new PaginatedList<GlossaryTermDto>(items, totalCount, request.Page, request.PageSize);
        return ApiResponse<PaginatedList<GlossaryTermDto>>.Ok(result);
    }
}
