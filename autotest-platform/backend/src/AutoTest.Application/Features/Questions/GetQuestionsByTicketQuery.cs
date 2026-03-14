using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.Questions;

public record GetQuestionsByTicketQuery(
    int TicketNumber,
    Language Language = Language.UzLatin) : IRequest<ApiResponse<List<QuestionSummaryDto>>>;

public class GetQuestionsByTicketQueryHandler(
    IApplicationDbContext db,
    IFileStorageService storage) : IRequestHandler<GetQuestionsByTicketQuery, ApiResponse<List<QuestionSummaryDto>>>
{
    public async Task<ApiResponse<List<QuestionSummaryDto>>> Handle(GetQuestionsByTicketQuery request, CancellationToken ct)
    {
        var questions = await db.Questions
            .AsNoTracking()
            .Where(q => q.TicketNumber == request.TicketNumber && q.IsActive)
            .Include(q => q.AnswerOptions)
            .Include(q => q.Category)
            .OrderBy(q => q.Id)
            .ToListAsync(ct);

        // Batch presigned URL generation — single parallel call instead of N+1
        var allImageKeys = new List<string>();
        foreach (var q in questions)
        {
            if (q.ImageUrl is not null) allImageKeys.Add(q.ImageUrl);
            foreach (var a in q.AnswerOptions)
                if (a.ImageUrl is not null) allImageKeys.Add(a.ImageUrl);
        }
        var urlMap = await storage.GetPresignedUrlsBatchAsync(allImageKeys, ct);

        var dtos = questions.Select(q =>
        {
            var imageUrl = q.ImageUrl is not null ? urlMap.GetValueOrDefault(q.ImageUrl) : null;

            var options = q.AnswerOptions.Select(a =>
            {
                var optImg = a.ImageUrl is not null ? urlMap.GetValueOrDefault(a.ImageUrl) : null;
                return new AnswerOptionDto(a.Id, a.Text, optImg);
            }).ToList();

            return new QuestionSummaryDto(q.Id, q.Text, imageUrl,
                q.Difficulty, q.CategoryId, q.Category?.Name ?? new LocalizedText("", "", ""),
                q.TicketNumber, options);
        }).ToList();

        return ApiResponse<List<QuestionSummaryDto>>.Ok(dtos);
    }
}
