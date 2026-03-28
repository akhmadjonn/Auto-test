using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.Glossary;

public record GetGlossaryTermByIdQuery(Guid Id) : IRequest<ApiResponse<GlossaryTermDetailDto>>;

public record GlossaryTermDetailDto(
    Guid Id,
    LocalizedText Term,
    LocalizedText Definition,
    int SortOrder,
    Guid[] RelatedQuestionIds,
    List<RelatedQuestionDto> RelatedQuestions);

public record RelatedQuestionDto(Guid Id, string TextSnippet);

public class GetGlossaryTermByIdQueryValidator : AbstractValidator<GetGlossaryTermByIdQuery>
{
    public GetGlossaryTermByIdQueryValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class GetGlossaryTermByIdQueryHandler(
    IApplicationDbContext db) : IRequestHandler<GetGlossaryTermByIdQuery, ApiResponse<GlossaryTermDetailDto>>
{
    public async Task<ApiResponse<GlossaryTermDetailDto>> Handle(GetGlossaryTermByIdQuery request, CancellationToken ct)
    {
        var term = await db.GlossaryTerms
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.Id, ct);

        if (term is null)
            return ApiResponse<GlossaryTermDetailDto>.Fail("TERM_NOT_FOUND", "Glossary term not found.");

        var relatedQuestions = new List<RelatedQuestionDto>();
        if (term.RelatedQuestionIds.Length > 0)
        {
            var questions = await db.Questions
                .AsNoTracking()
                .Where(q => term.RelatedQuestionIds.Contains(q.Id))
                .ToListAsync(ct);

            relatedQuestions = questions.Select(q => new RelatedQuestionDto(
                q.Id,
                q.Text.UzLatin.Length > 100 ? q.Text.UzLatin[..100] : q.Text.UzLatin)).ToList();
        }

        var dto = new GlossaryTermDetailDto(
            term.Id,
            term.Term,
            term.Definition,
            term.SortOrder,
            term.RelatedQuestionIds,
            relatedQuestions);

        return ApiResponse<GlossaryTermDetailDto>.Ok(dto);
    }
}
