using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Exams;

public enum MarafonScope
{
    All = 0,
    Category = 1,
    TicketRange = 2
}

public record StartMarathonCommand(
    LicenseCategory LicenseCategory = LicenseCategory.AB,
    Language Language = Language.UzLatin,
    MarafonScope Scope = MarafonScope.All,
    Guid? CategoryId = null,
    int? TicketFrom = null,
    int? TicketTo = null) : IRequest<ApiResponse<ExamSessionDto>>;

public class StartMarathonCommandValidator : AbstractValidator<StartMarathonCommand>
{
    public StartMarathonCommandValidator()
    {
        When(x => x.Scope == MarafonScope.Category, () =>
            RuleFor(x => x.CategoryId)
                .NotNull().WithMessage("CategoryId is required when Scope=Category.")
                .NotEqual(Guid.Empty));

        When(x => x.Scope == MarafonScope.TicketRange, () =>
        {
            RuleFor(x => x.TicketFrom)
                .NotNull().WithMessage("TicketFrom is required when Scope=TicketRange.")
                .GreaterThan(0);
            RuleFor(x => x.TicketTo)
                .NotNull().WithMessage("TicketTo is required when Scope=TicketRange.")
                .GreaterThanOrEqualTo(x => x.TicketFrom ?? 0);
        });
    }
}

public class StartMarathonCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IFileStorageService storage,
    IDateTimeProvider dateTime,
    ILogger<StartMarathonCommandHandler> logger) : IRequestHandler<StartMarathonCommand, ApiResponse<ExamSessionDto>>
{
    private const int InitialBatchSize = 20;

    public async Task<ApiResponse<ExamSessionDto>> Handle(StartMarathonCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return ApiResponse<ExamSessionDto>.Fail("UNAUTHORIZED", "Not authenticated.");

        var userId = currentUser.UserId.Value;
        var now = dateTime.UtcNow;

        // Resume an existing active marafon (or block if any other timed mode is active/paused)
        var existing = await db.ExamSessions
            .Include(s => s.SessionQuestions)
            .FirstOrDefaultAsync(s => s.UserId == userId
                && (s.Status == ExamStatus.InProgress || s.Status == ExamStatus.Paused), ct);

        if (existing is not null && existing.Mode == ExamMode.Marathon)
        {
            return ApiResponse<ExamSessionDto>.Ok(new ExamSessionDto(
                existing.Id, "inProgress", existing.SessionQuestions.Count, 0,
                0, null, "marathon", null, []));
        }

        if (existing is not null)
            return ApiResponse<ExamSessionDto>.Fail("ACTIVE_SESSION_EXISTS",
                "You already have an active exam session. Complete or abandon it first.");

        // Build the question pool per scope. Filters compose with license-category narrowing.
        var query = db.Questions
            .AsNoTracking()
            .Include(q => q.AnswerOptions)
            .Where(q => q.Status == QuestionStatus.Active
                && (q.LicenseCategory == request.LicenseCategory || q.LicenseCategory == LicenseCategory.Both));

        switch (request.Scope)
        {
            case MarafonScope.Category:
                query = query.Where(q => q.CategoryId == request.CategoryId!.Value);
                break;
            case MarafonScope.TicketRange:
                var from = request.TicketFrom!.Value;
                var to = request.TicketTo!.Value;
                query = query.Where(q => q.TicketNumber >= from && q.TicketNumber <= to);
                break;
            case MarafonScope.All:
            default:
                break;
        }

        var questions = await query
            .OrderBy(q => q.TicketNumber)
            .ThenBy(q => q.Id)
            .ToListAsync(ct);

        if (questions.Count == 0)
            return ApiResponse<ExamSessionDto>.Fail("NO_QUESTIONS",
                "No questions matched the selected scope.");

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

        // Ship only the first batch — remaining are fetched via GET /exams/{id}/questions
        var firstBatch = questions.Take(InitialBatchSize).ToList();

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

        logger.LogInformation(
            "Marathon started: session {SessionId} for user {UserId}, scope={Scope}, total={Total}",
            session.Id, userId, request.Scope, questions.Count);

        return ApiResponse<ExamSessionDto>.Ok(new ExamSessionDto(
            session.Id, "inProgress", questions.Count, 0,
            0, null, "marathon", null, questionDtos));
    }
}
