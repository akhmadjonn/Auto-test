using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.Questions;

public record GetAdminQuestionsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    Difficulty? Difficulty = null,
    bool? IsActive = null,
    int? TicketNumber = null,
    Guid? CategoryId = null) : IRequest<ApiResponse<PaginatedList<AdminQuestionListDto>>>;

public record AdminQuestionListDto(
    Guid Id,
    LocalizedText Text,
    LocalizedText Explanation,
    string? ImageUrl,
    string? ThumbnailUrl,
    Guid CategoryId,
    LocalizedText CategoryName,
    Difficulty Difficulty,
    int TicketNumber,
    LicenseCategory LicenseCategory,
    bool IsActive,
    DateTimeOffset CreatedAt,
    List<AdminAnswerOptionDto> AnswerOptions);

public record AdminAnswerOptionDto(Guid Id, LocalizedText Text, string? ImageUrl, bool IsCorrect);

public class GetAdminQuestionsQueryValidator : AbstractValidator<GetAdminQuestionsQuery>
{
    public GetAdminQuestionsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public class GetAdminQuestionsQueryHandler(
    IApplicationDbContext db,
    IFileStorageService storage) : IRequestHandler<GetAdminQuestionsQuery, ApiResponse<PaginatedList<AdminQuestionListDto>>>
{
    public async Task<ApiResponse<PaginatedList<AdminQuestionListDto>>> Handle(GetAdminQuestionsQuery request, CancellationToken ct)
    {
        var query = db.Questions
            .AsNoTracking()
            .Include(q => q.Category)
            .Include(q => q.AnswerOptions)
            .AsQueryable();

        if (request.CategoryId.HasValue)
            query = query.Where(q => q.CategoryId == request.CategoryId.Value);

        if (request.Difficulty.HasValue)
            query = query.Where(q => q.Difficulty == request.Difficulty.Value);

        if (request.IsActive.HasValue)
            query = query.Where(q => q.IsActive == request.IsActive.Value);

        if (request.TicketNumber.HasValue)
            query = query.Where(q => q.TicketNumber == request.TicketNumber.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(q =>
                q.Text.UzLatin.ToLower().Contains(search) ||
                q.Text.Uz.ToLower().Contains(search) ||
                q.Text.Ru.ToLower().Contains(search));
        }

        var total = await query.CountAsync(ct);

        var questions = await query
            .OrderByDescending(q => q.CreatedAt)
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
                return new AdminAnswerOptionDto(a.Id, a.Text, optImg, a.IsCorrect);
            }));

            return new AdminQuestionListDto(
                q.Id, q.Text, q.Explanation, imageUrl, thumbUrl,
                q.CategoryId, q.Category?.Name ?? new LocalizedText("", "", ""),
                q.Difficulty, q.TicketNumber, q.LicenseCategory,
                q.IsActive, q.CreatedAt, [.. options]);
        }));

        var paginated = new PaginatedList<AdminQuestionListDto>([.. dtos], total, request.Page, request.PageSize);
        return ApiResponse<PaginatedList<AdminQuestionListDto>>.Ok(paginated);
    }
}
