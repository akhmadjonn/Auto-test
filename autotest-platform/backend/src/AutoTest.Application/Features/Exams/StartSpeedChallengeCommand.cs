using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Exams;

public record StartSpeedChallengeCommand(
    LicenseCategory LicenseCategory,
    Language Language = Language.UzLatin) : IRequest<ApiResponse<ExamSessionDto>>;

public class StartSpeedChallengeCommandValidator : AbstractValidator<StartSpeedChallengeCommand>
{
    public StartSpeedChallengeCommandValidator()
    {
        RuleFor(x => x.LicenseCategory).IsInEnum();
    }
}

public class StartSpeedChallengeCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IFileStorageService storage,
    IDistributedLockService lockService,
    IDateTimeProvider dateTime,
    ILogger<StartSpeedChallengeCommandHandler> logger) : IRequestHandler<StartSpeedChallengeCommand, ApiResponse<ExamSessionDto>>
{
    public async Task<ApiResponse<ExamSessionDto>> Handle(StartSpeedChallengeCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return ApiResponse<ExamSessionDto>.Fail("UNAUTHORIZED", "Not authenticated.");

        var userId = currentUser.UserId.Value;

        await using var lockHandle = await lockService.TryAcquireAsync(
            $"avtolider:lock:exam:{userId}", TimeSpan.FromSeconds(10), ct);
        if (lockHandle is null)
            return ApiResponse<ExamSessionDto>.Fail("CONCURRENT_REQUEST", "Another exam start is in progress.");

        // Speed challenge requires premium
        var now = dateTime.UtcNow;
        var hasSubscription = await db.Subscriptions
            .AnyAsync(s => s.UserId == userId && s.Status == SubscriptionStatus.Active && s.ExpiresAt > now, ct);

        if (!hasSubscription)
            return ApiResponse<ExamSessionDto>.Fail("PREMIUM_REQUIRED", "Speed Challenge requires a premium subscription.");

        // Prevent concurrent active sessions
        var hasActiveSession = await db.ExamSessions
            .AnyAsync(s => s.UserId == userId && s.Status == ExamStatus.InProgress, ct);
        if (hasActiveSession)
            return ApiResponse<ExamSessionDto>.Fail("ACTIVE_SESSION_EXISTS",
                "You already have an active exam session. Complete or abandon it first.");

        // Find speed challenge template (one with TimeLimitPerQuestionSeconds set)
        var template = await db.ExamTemplates
            .Include(t => t.PoolRules)
            .Where(t => t.IsActive && t.TimeLimitPerQuestionSeconds.HasValue)
            .FirstOrDefaultAsync(ct);

        if (template is null)
            return ApiResponse<ExamSessionDto>.Fail("TEMPLATE_NOT_FOUND", "Speed challenge template not found.");

        // Select random questions per pool rules
        List<Question> selectedQuestions = [];
        foreach (var rule in template.PoolRules)
        {
            var poolQuery = db.Questions
                .AsNoTracking()
                .Include(q => q.AnswerOptions)
                .Where(q => q.CategoryId == rule.CategoryId && q.Status == QuestionStatus.Active);

            if (rule.Difficulty.HasValue)
                poolQuery = poolQuery.Where(q => q.Difficulty == rule.Difficulty.Value);

            if (request.LicenseCategory != LicenseCategory.Both)
                poolQuery = poolQuery.Where(q => q.LicenseCategory == request.LicenseCategory
                    || q.LicenseCategory == LicenseCategory.Both);

            var pool = await poolQuery
                .OrderBy(q => EF.Functions.Random())
                .Take(rule.QuestionCount)
                .ToListAsync(ct);

            selectedQuestions.AddRange(pool);
        }

        if (selectedQuestions.Count == 0)
            return ApiResponse<ExamSessionDto>.Fail("NO_QUESTIONS", "No questions available for speed challenge.");

        var shuffled = selectedQuestions.OrderBy(_ => Random.Shared.Next()).ToList();

        var perQuestionSeconds = template.TimeLimitPerQuestionSeconds!.Value;
        var totalSeconds = shuffled.Count * perQuestionSeconds;
        var expiresAt = now.AddSeconds(totalSeconds);

        var session = new ExamSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ExamTemplateId = template.Id,
            Status = ExamStatus.InProgress,
            Mode = ExamMode.SpeedChallenge,
            LicenseCategory = request.LicenseCategory,
            ExpiresAt = expiresAt,
            TimeLimitPerQuestionSeconds = perQuestionSeconds,
            CreatedAt = now,
            UpdatedAt = now
        };

        var sessionQuestions = shuffled.Select((q, idx) => new SessionQuestion
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

        // Batch presigned URLs
        var allImageKeys = new List<string>();
        foreach (var q in shuffled)
        {
            if (q.ImageUrl is not null) allImageKeys.Add(q.ImageUrl);
            foreach (var a in q.AnswerOptions)
                if (a.ImageUrl is not null) allImageKeys.Add(a.ImageUrl);
        }

        var urlMap = await storage.GetPresignedUrlsBatchAsync(allImageKeys, ct);

        var questionDtos = shuffled.Select((q, idx) =>
        {
            var imgUrl = q.ImageUrl is not null ? urlMap.GetValueOrDefault(q.ImageUrl) : null;
            var shuffledOptions = q.AnswerOptions.OrderBy(_ => Random.Shared.Next()).ToList();
            var optDtos = shuffledOptions.Select(a =>
            {
                var optImg = a.ImageUrl is not null ? urlMap.GetValueOrDefault(a.ImageUrl) : null;
                return new ExamAnswerOptionDto(a.Id, a.Text, optImg);
            }).ToList();

            return new ExamQuestionDto(
                sessionQuestions[idx].Id,
                q.Id,
                idx + 1,
                q.Text,
                imgUrl,
                optDtos);
        }).ToList();

        logger.LogInformation("Speed challenge started: session {SessionId} for user {UserId}, {Count} questions, {Seconds}s/question",
            session.Id, userId, shuffled.Count, perQuestionSeconds);

        return ApiResponse<ExamSessionDto>.Ok(new ExamSessionDto(
            session.Id,
            "inProgress",
            shuffled.Count,
            template.PassingScore,
            template.TimeLimitMinutes,
            expiresAt,
            "speedChallenge",
            null,
            questionDtos)
        {
            TimeLimitPerQuestionSeconds = perQuestionSeconds
        });
    }
}
