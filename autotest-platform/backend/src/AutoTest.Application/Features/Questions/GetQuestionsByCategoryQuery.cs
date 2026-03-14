using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;
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
    LocalizedText Text,
    string? ImageUrl,
    Difficulty Difficulty,
    Guid CategoryId,
    LocalizedText CategoryName,
    int TicketNumber,
    List<AnswerOptionDto> AnswerOptions);

public record AnswerOptionDto(Guid Id, LocalizedText Text, string? ImageUrl);

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

        var query = baseQuery.Include(q => q.AnswerOptions).Include(q => q.Category);

        var questions = await query
            .OrderBy(q => q.TicketNumber)
            .ThenBy(q => q.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var dtos = await Task.WhenAll(questions.Select(async q =>
        {
            var imageUrl = q.ImageUrl is not null ? await storage.GetPresignedUrlAsync(q.ImageUrl, ct) : null;

            var options = await Task.WhenAll(q.AnswerOptions.Select(async a =>
            {
                var optImg = a.ImageUrl is not null ? await storage.GetPresignedUrlAsync(a.ImageUrl, ct) : null;
                return new AnswerOptionDto(a.Id, a.Text, optImg);
            }));

            return new QuestionSummaryDto(q.Id, q.Text, imageUrl,
                q.Difficulty, q.CategoryId, q.Category?.Name ?? new LocalizedText("", "", ""),
                q.TicketNumber, [..options]);
        }));

        var paginated = new PaginatedList<QuestionSummaryDto>([..dtos], total, request.Page, request.PageSize);
        return ApiResponse<PaginatedList<QuestionSummaryDto>>.Ok(paginated);
    }
}
