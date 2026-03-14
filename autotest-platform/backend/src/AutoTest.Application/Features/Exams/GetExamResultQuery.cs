using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.Exams;

public record GetExamResultQuery(Guid SessionId, Language Language = Language.UzLatin)
    : IRequest<ApiResponse<ExamResultDto>>;

public class GetExamResultQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IFileStorageService storage) : IRequestHandler<GetExamResultQuery, ApiResponse<ExamResultDto>>
{
    public async Task<ApiResponse<ExamResultDto>> Handle(GetExamResultQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return ApiResponse<ExamResultDto>.Fail("UNAUTHORIZED", "Not authenticated.");

        var session = await db.ExamSessions
            .Include(s => s.SessionQuestions)
                .ThenInclude(sq => sq.Question)
                    .ThenInclude(q => q.AnswerOptions)
            .Include(s => s.ExamTemplate)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId
                && s.UserId == currentUser.UserId
                && s.Status == ExamStatus.Completed, ct);

        if (session is null)
            return ApiResponse<ExamResultDto>.Fail("RESULT_NOT_FOUND", "Completed session not found.");

        var passingScore = session.ExamTemplate?.PassingScore ?? 80;
        var total = session.SessionQuestions.Count;

        // Batch presigned URL generation — single parallel call instead of N+1
        var orderedQuestions = session.SessionQuestions.OrderBy(sq => sq.Order).ToList();
        var allImageKeys = new List<string>();
        foreach (var sq in orderedQuestions)
        {
            if (sq.Question.ImageUrl is not null) allImageKeys.Add(sq.Question.ImageUrl);
            foreach (var a in sq.Question.AnswerOptions)
                if (a.ImageUrl is not null) allImageKeys.Add(a.ImageUrl);
        }
        var urlMap = await storage.GetPresignedUrlsBatchAsync(allImageKeys, ct);

        var questionDtos = orderedQuestions.Select(sq =>
        {
            var q = sq.Question;
            var imgUrl = q.ImageUrl is not null ? urlMap.GetValueOrDefault(q.ImageUrl) : null;
            var correctOptionId = q.AnswerOptions.FirstOrDefault(a => a.IsCorrect)?.Id;

            var optDtos = q.AnswerOptions.Select(a =>
            {
                var optImg = a.ImageUrl is not null ? urlMap.GetValueOrDefault(a.ImageUrl) : null;
                return new ExamResultOptionDto(a.Id, a.Text, a.IsCorrect, optImg);
            }).ToList();

            return new ExamResultQuestionDto(
                q.Id, q.Text, imgUrl,
                q.Explanation,
                sq.SelectedAnswerId, correctOptionId, sq.IsCorrect,
                sq.TimeSpentSeconds, optDtos);
        }).ToList();

        return ApiResponse<ExamResultDto>.Ok(new ExamResultDto(
            session.Id, total, session.CorrectAnswers ?? 0,
            session.Score ?? 0, passingScore,
            (session.Score ?? 0) >= passingScore,
            session.TimeTakenSeconds, session.CompletedAt, questionDtos));
    }
}
