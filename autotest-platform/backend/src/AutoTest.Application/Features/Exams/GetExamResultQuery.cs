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

        var questionDtos = await Task.WhenAll(session.SessionQuestions
            .OrderBy(sq => sq.Order)
            .Select(async sq =>
            {
                var q = sq.Question;
                var imgUrl = q.ImageUrl is not null ? await storage.GetPresignedUrlAsync(q.ImageUrl, ct) : null;

                var optDtos = await Task.WhenAll(q.AnswerOptions.Select(async a =>
                {
                    var optImg = a.ImageUrl is not null ? await storage.GetPresignedUrlAsync(a.ImageUrl, ct) : null;
                    return new ExamResultOptionDto(a.Id, a.Text.Get(request.Language), a.IsCorrect, optImg);
                }));

                return new ExamResultQuestionDto(
                    q.Id, q.Text.Get(request.Language), imgUrl,
                    q.Explanation.Get(request.Language),
                    sq.SelectedAnswerId, sq.IsCorrect, [..optDtos]);
            }));

        return ApiResponse<ExamResultDto>.Ok(new ExamResultDto(
            session.Id, total, session.CorrectAnswers ?? 0,
            session.Score ?? 0, (session.Score ?? 0) >= passingScore,
            session.TimeTakenSeconds, [..questionDtos]));
    }
}
