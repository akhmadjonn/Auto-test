using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.Exams;

public record GetExamSessionQuery(Guid SessionId, Language Language = Language.UzLatin)
    : IRequest<ApiResponse<ExamSessionDto>>;

public class GetExamSessionQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IFileStorageService storage) : IRequestHandler<GetExamSessionQuery, ApiResponse<ExamSessionDto>>
{
    public async Task<ApiResponse<ExamSessionDto>> Handle(GetExamSessionQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return ApiResponse<ExamSessionDto>.Fail("UNAUTHORIZED", "Not authenticated.");

        var session = await db.ExamSessions
            .Include(s => s.SessionQuestions)
                .ThenInclude(sq => sq.Question)
                    .ThenInclude(q => q.AnswerOptions)
            .Include(s => s.ExamTemplate)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId && s.UserId == currentUser.UserId, ct);

        if (session is null)
            return ApiResponse<ExamSessionDto>.Fail("SESSION_NOT_FOUND", "Session not found.");

        var timeLimitMinutes = session.ExamTemplate?.TimeLimitMinutes ?? 20;
        var passingScore = session.ExamTemplate?.PassingScore ?? 80;
        var mode = session.Mode switch
        {
            ExamMode.Exam => "exam",
            ExamMode.Ticket => "ticket",
            ExamMode.Marathon => "marathon",
            _ => "exam"
        };

        // Batch presigned URL generation — single parallel call instead of N+1
        var allImageKeys = new List<string>();
        foreach (var sq in session.SessionQuestions)
        {
            if (sq.Question.ImageUrl is not null) allImageKeys.Add(sq.Question.ImageUrl);
            foreach (var a in sq.Question.AnswerOptions)
                if (a.ImageUrl is not null) allImageKeys.Add(a.ImageUrl);
        }

        var urlMap = await storage.GetPresignedUrlsBatchAsync(allImageKeys, ct);

        var questionDtos = session.SessionQuestions
            .OrderBy(sq => sq.Order)
            .Select(sq =>
            {
                var q = sq.Question;
                var imgUrl = q.ImageUrl is not null ? urlMap.GetValueOrDefault(q.ImageUrl) : null;
                var optDtos = q.AnswerOptions.Select(a =>
                {
                    var optImg = a.ImageUrl is not null ? urlMap.GetValueOrDefault(a.ImageUrl) : null;
                    return new ExamAnswerOptionDto(a.Id, a.Text, optImg);
                }).ToList();

                return new ExamQuestionDto(sq.Id, q.Id, sq.Order, q.Text, imgUrl, optDtos, sq.SelectedAnswerId);
            }).ToList();

        return ApiResponse<ExamSessionDto>.Ok(new ExamSessionDto(
            session.Id,
            session.Status switch
            {
                ExamStatus.InProgress => "inProgress",
                ExamStatus.Completed => "completed",
                ExamStatus.Expired => "expired",
                ExamStatus.Abandoned => "abandoned",
                _ => "completed"
            },
            session.SessionQuestions.Count,
            passingScore,
            timeLimitMinutes,
            session.ExpiresAt,
            mode,
            session.TicketNumber,
            questionDtos));
    }
}
