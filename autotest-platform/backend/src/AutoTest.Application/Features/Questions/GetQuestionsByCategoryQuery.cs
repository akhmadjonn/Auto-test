using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.Questions;

public record GetQuestionsByCategoryQuery(
    Guid CategoryId,
    Language Language = Language.UzLatin,
    int Page = 1,
    int PageSize = 20,
    Difficulty? Difficulty = null,
    LicenseCategory? LicenseCategory = null) : IRequest<ApiResponse<PaginatedList<QuestionSummaryDto>>>;

public record QuestionSummaryDto(
    Guid Id,
    string Text,
    string? ImageUrl,
    string? ThumbnailUrl,
    Difficulty Difficulty,
    int TicketNumber,
    bool IsActive,
    List<AnswerOptionDto> AnswerOptions);

public record AnswerOptionDto(Guid Id, string Text, string? ImageUrl);

public class GetQuestionsByCategoryQueryHandler(
    IApplicationDbContext db,
    IFileStorageService storage) : IRequestHandler<GetQuestionsByCategoryQuery, ApiResponse<PaginatedList<QuestionSummaryDto>>>
{
    public async Task<ApiResponse<PaginatedList<QuestionSummaryDto>>> Handle(GetQuestionsByCategoryQuery request, CancellationToken ct)
    {
        var baseQuery = db.Questions
            .AsNoTracking()
            .Where(q => q.CategoryId == request.CategoryId && q.IsActive);

        if (request.Difficulty.HasValue)
            baseQuery = baseQuery.Where(q => q.Difficulty == request.Difficulty.Value);

        if (request.LicenseCategory.HasValue)
            baseQuery = baseQuery.Where(q => q.LicenseCategory == request.LicenseCategory.Value
                || q.LicenseCategory == Domain.Common.Enums.LicenseCategory.Both);

        var total = await baseQuery.CountAsync(ct);

        var query = baseQuery.Include(q => q.AnswerOptions);

        var questions = await query
            .OrderBy(q => q.TicketNumber)
            .ThenBy(q => q.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var dtos = await Task.WhenAll(questions.Select(async q =>
        {
            var imageUrl = q.ImageUrl is not null ? await storage.GetPresignedUrlAsync(q.ImageUrl, ct) : null;
            var thumbUrl = q.ThumbnailUrl is not null ? await storage.GetPresignedUrlAsync(q.ThumbnailUrl, ct) : null;

            var options = await Task.WhenAll(q.AnswerOptions.Select(async a =>
            {
                var optImg = a.ImageUrl is not null ? await storage.GetPresignedUrlAsync(a.ImageUrl, ct) : null;
                return new AnswerOptionDto(a.Id, a.Text.Get(request.Language), optImg);
            }));

            return new QuestionSummaryDto(q.Id, q.Text.Get(request.Language), imageUrl, thumbUrl,
                q.Difficulty, q.TicketNumber, q.IsActive, [..options]);
        }));

        var paginated = new PaginatedList<QuestionSummaryDto>([..dtos], total, request.Page, request.PageSize);
        return ApiResponse<PaginatedList<QuestionSummaryDto>>.Ok(paginated);
    }
}
