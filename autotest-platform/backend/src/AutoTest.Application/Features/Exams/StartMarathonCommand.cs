using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Exams;

public record StartMarathonCommand(
    LicenseCategory LicenseCategory = LicenseCategory.AB,
    Language Language = Language.UzLatin) : IRequest<ApiResponse<ExamSessionDto>>;

public class StartMarathonCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IFileStorageService storage,
    IDateTimeProvider dateTime,
    ILogger<StartMarathonCommandHandler> logger) : IRequestHandler<StartMarathonCommand, ApiResponse<ExamSessionDto>>
{
    public async Task<ApiResponse<ExamSessionDto>> Handle(StartMarathonCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return ApiResponse<ExamSessionDto>.Fail("UNAUTHORIZED", "Not authenticated.");

        var userId = currentUser.UserId.Value;
        var now = dateTime.UtcNow;

        // Check for existing active marathon — resume it
        var existing = await db.ExamSessions
            .Include(s => s.SessionQuestions)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == ExamStatus.InProgress, ct);

        if (existing is not null && existing.Mode == ExamMode.Marathon)
        {
            return ApiResponse<ExamSessionDto>.Ok(new ExamSessionDto(
                existing.Id, "inProgress", existing.SessionQuestions.Count, 0,
                0, null, "marathon", null, []));
        }

        if (existing is not null)
            return ApiResponse<ExamSessionDto>.Fail("ACTIVE_SESSION_EXISTS",
                "You already have an active exam session. Complete or abandon it first.");

        // Load ALL active questions in order
        var questions = await db.Questions
            .AsNoTracking()
            .Include(q => q.AnswerOptions)
            .Where(q => q.IsActive && (q.LicenseCategory == request.LicenseCategory || q.LicenseCategory == LicenseCategory.Both))
            .OrderBy(q => q.TicketNumber)
            .ThenBy(q => q.Id)
            .ToListAsync(ct);

        if (questions.Count == 0)
            return ApiResponse<ExamSessionDto>.Fail("NO_QUESTIONS", "No questions available.");

        var session = new ExamSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Status = ExamStatus.InProgress,
            Mode = ExamMode.Marathon,
            LicenseCategory = request.LicenseCategory,
            ExpiresAt = null,
            CreatedAt = now,
            UpdatedAt = now
        };

        var sessionQuestions = questions.Select((q, idx) => new SessionQuestion
        {
            Id = Guid.NewGuid(),
            ExamSessionId = session.Id,
            QuestionId = q.Id,
            Order = idx + 1,
            CreatedAt = now,
            UpdatedAt = now
        }).ToList();

        session.SessionQuestions = sessionQuestions;
        db.ExamSessions.Add(session);
        await db.SaveChangesAsync(ct);

        // Return first batch of questions (up to 20)
        var firstBatch = questions.Take(20).ToList();

        // Batch presigned URL generation — single parallel call instead of N+1
        var allImageKeys = new List<string>();
        foreach (var q in firstBatch)
        {
            if (q.ImageUrl is not null) allImageKeys.Add(q.ImageUrl);
            foreach (var a in q.AnswerOptions)
                if (a.ImageUrl is not null) allImageKeys.Add(a.ImageUrl);
        }
        var urlMap = await storage.GetPresignedUrlsBatchAsync(allImageKeys, ct);

        var questionDtos = firstBatch.Select((q, idx) =>
        {
            var imgUrl = q.ImageUrl is not null ? urlMap.GetValueOrDefault(q.ImageUrl) : null;
            var shuffledOptions = q.AnswerOptions.OrderBy(_ => Random.Shared.Next()).ToList();

            var optDtos = shuffledOptions.Select(a =>
            {
                var optImg = a.ImageUrl is not null ? urlMap.GetValueOrDefault(a.ImageUrl) : null;
                return new ExamAnswerOptionDto(a.Id, a.Text, optImg);
            }).ToList();

            return new ExamQuestionDto(
                sessionQuestions[idx].Id, q.Id, idx + 1,
                q.Text, imgUrl, optDtos);
        }).ToList();

        logger.LogInformation("Marathon started: session {SessionId} for user {UserId}, {Total} questions",
            session.Id, userId, questions.Count);

        return ApiResponse<ExamSessionDto>.Ok(new ExamSessionDto(
            session.Id, "inProgress", questions.Count, 0,
            0, null, "marathon", null, questionDtos));
    }
}
