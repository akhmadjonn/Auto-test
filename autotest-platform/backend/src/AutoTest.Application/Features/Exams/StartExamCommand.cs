using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Exams;

public record StartExamCommand(
    Guid ExamTemplateId,
    LicenseCategory LicenseCategory,
    Language Language = Language.UzLatin) : IRequest<ApiResponse<ExamSessionDto>>;

public record ExamSessionDto(
    Guid SessionId,
    int TotalQuestions,
    int TimeLimitMinutes,
    DateTimeOffset? ExpiresAt,
    List<ExamQuestionDto> Questions);

public record ExamQuestionDto(
    Guid SessionQuestionId,
    Guid QuestionId,
    int Order,
    string Text,
    string? ImageUrl,
    List<ExamAnswerOptionDto> Options);

public record ExamAnswerOptionDto(Guid Id, string Text, string? ImageUrl);

public class StartExamCommandValidator : AbstractValidator<StartExamCommand>
{
    public StartExamCommandValidator()
    {
        RuleFor(x => x.ExamTemplateId).NotEmpty();
    }
}

public class StartExamCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IFileStorageService storage,
    ICacheService cache,
    IDistributedLockService lockService,
    IDateTimeProvider dateTime,
    ILogger<StartExamCommandHandler> logger) : IRequestHandler<StartExamCommand, ApiResponse<ExamSessionDto>>
{
    public async Task<ApiResponse<ExamSessionDto>> Handle(StartExamCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return ApiResponse<ExamSessionDto>.Fail("UNAUTHORIZED", "Not authenticated.");

        var userId = currentUser.UserId.Value;

        // Distributed lock prevents concurrent exam starts for the same user
        await using var lockHandle = await lockService.TryAcquireAsync(
            $"avtolider:lock:exam:{userId}", TimeSpan.FromSeconds(10), ct);
        if (lockHandle is null)
            return ApiResponse<ExamSessionDto>.Fail("CONCURRENT_REQUEST", "Another exam start is in progress.");

        // Check free daily limit vs subscription
        var now = dateTime.UtcNow;
        var hasSubscription = await db.Subscriptions
            .AnyAsync(s => s.UserId == userId && s.Status == SubscriptionStatus.Active && s.ExpiresAt > now, ct);

        // Prevent multiple concurrent active sessions (max_active_sessions = 1)
        var hasActiveSession = await db.ExamSessions
            .AnyAsync(s => s.UserId == userId && s.Status == ExamStatus.InProgress, ct);
        if (hasActiveSession)
            return ApiResponse<ExamSessionDto>.Fail("ACTIVE_SESSION_EXISTS",
                "You already have an active exam session. Complete or abandon it first.");

        if (!hasSubscription)
        {
            var limitSetting = await cache.GetAsync<string>("avtolider:settings:free_daily_exam_limit", ct);
            var limit = int.TryParse(limitSetting, out var l) ? l : 3;

            var today = now.Date;
            var todayExams = await db.ExamSessions
                .CountAsync(s => s.UserId == userId
                    && s.Mode == ExamMode.Exam
                    && s.CreatedAt >= today, ct);

            if (todayExams >= limit)
                return ApiResponse<ExamSessionDto>.Fail("DAILY_LIMIT_REACHED",
                    $"Free daily exam limit ({limit}) reached. Subscribe to continue.");
        }

        // Load template + pool rules
        var template = await db.ExamTemplates
            .Include(t => t.PoolRules)
            .FirstOrDefaultAsync(t => t.Id == request.ExamTemplateId && t.IsActive, ct);

        if (template is null)
            return ApiResponse<ExamSessionDto>.Fail("TEMPLATE_NOT_FOUND", "Exam template not found.");

        // Select random questions per pool rules
        var selectedQuestions = new List<Question>();
        foreach (var rule in template.PoolRules)
        {
            var poolQuery = db.Questions
                .AsNoTracking()
                .Include(q => q.AnswerOptions)
                .Where(q => q.CategoryId == rule.CategoryId && q.IsActive);

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
            return ApiResponse<ExamSessionDto>.Fail("NO_QUESTIONS", "No questions available for this exam.");

        // Shuffle questions
        var shuffled = selectedQuestions.OrderBy(_ => Random.Shared.Next()).ToList();

        var expiresAt = now.AddMinutes(template.TimeLimitMinutes);
        var session = new ExamSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ExamTemplateId = template.Id,
            Status = ExamStatus.InProgress,
            Mode = ExamMode.Exam,
            LicenseCategory = request.LicenseCategory,
            ExpiresAt = expiresAt,
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

        // Build response WITHOUT correct answers
        var questionDtos = await Task.WhenAll(shuffled.Select(async (q, idx) =>
        {
            var imgUrl = q.ImageUrl is not null ? await storage.GetPresignedUrlAsync(q.ImageUrl, ct) : null;
            var shuffledOptions = q.AnswerOptions.OrderBy(_ => Random.Shared.Next()).ToList();

            var optDtos = await Task.WhenAll(shuffledOptions.Select(async a =>
            {
                var optImg = a.ImageUrl is not null ? await storage.GetPresignedUrlAsync(a.ImageUrl, ct) : null;
                return new ExamAnswerOptionDto(a.Id, a.Text.Get(request.Language), optImg);
            }));

            return new ExamQuestionDto(
                sessionQuestions[idx].Id,
                q.Id,
                idx + 1,
                q.Text.Get(request.Language),
                imgUrl,
                [..optDtos]);
        }));

        logger.LogInformation("Exam started: session {SessionId} for user {UserId}", session.Id, userId);
        return ApiResponse<ExamSessionDto>.Ok(new ExamSessionDto(
            session.Id,
            shuffled.Count,
            template.TimeLimitMinutes,
            expiresAt,
            [..questionDtos]));
    }
}
