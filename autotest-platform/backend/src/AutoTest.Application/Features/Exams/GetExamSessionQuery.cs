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

        var questionDtos = await Task.WhenAll(session.SessionQuestions
            .OrderBy(sq => sq.Order)
            .Select(async sq =>
            {
                var q = sq.Question;
                var imgUrl = q.ImageUrl is not null ? await storage.GetPresignedUrlAsync(q.ImageUrl, ct) : null;

                var optDtos = await Task.WhenAll(q.AnswerOptions.Select(async a =>
                {
                    var optImg = a.ImageUrl is not null ? await storage.GetPresignedUrlAsync(a.ImageUrl, ct) : null;
                    return new ExamAnswerOptionDto(a.Id, a.Text, optImg);
                }));

                return new ExamQuestionDto(sq.Id, q.Id, sq.Order, q.Text, imgUrl, [..optDtos], sq.SelectedAnswerId);
            }));

        return ApiResponse<ExamSessionDto>.Ok(new ExamSessionDto(
            session.Id,
            session.Status == ExamStatus.InProgress ? "inProgress" : "completed",
            session.SessionQuestions.Count,
            passingScore,
            timeLimitMinutes,
            session.ExpiresAt,
            mode,
            session.TicketNumber,
            [..questionDtos]));
    }
}
