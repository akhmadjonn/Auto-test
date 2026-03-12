using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.Questions;

public record GetQuestionByIdQuery(Guid Id) : IRequest<ApiResponse<QuestionDetailDto>>;

public record QuestionDetailDto(
    Guid Id,
    Guid CategoryId,
    string TextUz,
    string TextUzLatin,
    string TextRu,
    string ExplanationUz,
    string ExplanationUzLatin,
    string ExplanationRu,
    Difficulty Difficulty,
    int TicketNumber,
    LicenseCategory LicenseCategory,
    bool IsActive,
    string? ImageUrl,
    string? ThumbnailUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    List<AnswerOptionDetailDto> AnswerOptions);

public record AnswerOptionDetailDto(
    Guid Id,
    string TextUz,
    string TextUzLatin,
    string TextRu,
    bool IsCorrect,
    int SortOrder,
    string? ImageUrl);

public class GetQuestionByIdQueryHandler(
    IApplicationDbContext db,
    IFileStorageService storage) : IRequestHandler<GetQuestionByIdQuery, ApiResponse<QuestionDetailDto>>
{
    public async Task<ApiResponse<QuestionDetailDto>> Handle(GetQuestionByIdQuery request, CancellationToken ct)
    {
        var question = await db.Questions
            .AsNoTracking()
            .Include(q => q.AnswerOptions.OrderBy(a => a.SortOrder))
            .FirstOrDefaultAsync(q => q.Id == request.Id, ct);

        if (question is null)
            return ApiResponse<QuestionDetailDto>.Fail("QUESTION_NOT_FOUND", "Question not found.");

        var imageUrl = question.ImageUrl is not null
            ? await storage.GetPresignedUrlAsync(question.ImageUrl, ct)
            : null;
        var thumbUrl = question.ThumbnailUrl is not null
            ? await storage.GetPresignedUrlAsync(question.ThumbnailUrl, ct)
            : null;

        var options = new List<AnswerOptionDetailDto>();
        foreach (var opt in question.AnswerOptions)
        {
            var optImg = opt.ImageUrl is not null
                ? await storage.GetPresignedUrlAsync(opt.ImageUrl, ct)
                : null;

            options.Add(new AnswerOptionDetailDto(
                opt.Id,
                opt.Text.Uz,
                opt.Text.UzLatin,
                opt.Text.Ru,
                opt.IsCorrect,
                opt.SortOrder,
                optImg));
        }

        var dto = new QuestionDetailDto(
            question.Id,
            question.CategoryId,
            question.Text.Uz,
            question.Text.UzLatin,
            question.Text.Ru,
            question.Explanation.Uz,
            question.Explanation.UzLatin,
            question.Explanation.Ru,
            question.Difficulty,
            question.TicketNumber,
            question.LicenseCategory,
            question.IsActive,
            imageUrl,
            thumbUrl,
            question.CreatedAt,
            question.UpdatedAt,
            options);

        return ApiResponse<QuestionDetailDto>.Ok(dto);
    }
}
