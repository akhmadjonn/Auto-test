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

        return ApiResponse<List<QuestionSummaryDto>>.Ok([..dtos]);
    }
}
