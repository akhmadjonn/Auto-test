using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Exams;

public record StartTicketExamCommand(
    int TicketNumber,
    Language Language = Language.UzLatin) : IRequest<ApiResponse<ExamSessionDto>>;

public class StartTicketExamCommandValidator : AbstractValidator<StartTicketExamCommand>
{
    public StartTicketExamCommandValidator()
    {
        RuleFor(x => x.TicketNumber).GreaterThan(0);
    }
}

public class StartTicketExamCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IFileStorageService storage,
    IDateTimeProvider dateTime,
    ILogger<StartTicketExamCommandHandler> logger) : IRequestHandler<StartTicketExamCommand, ApiResponse<ExamSessionDto>>
{
    private const int TicketTimeLimitMinutes = 20;

    public async Task<ApiResponse<ExamSessionDto>> Handle(StartTicketExamCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return ApiResponse<ExamSessionDto>.Fail("UNAUTHORIZED", "Not authenticated.");

        var userId = currentUser.UserId.Value;
        var now = dateTime.UtcNow;

        var hasActiveSession = await db.ExamSessions
            .AnyAsync(s => s.UserId == userId && s.Status == ExamStatus.InProgress, ct);
        if (hasActiveSession)
            return ApiResponse<ExamSessionDto>.Fail("ACTIVE_SESSION_EXISTS",
                "You already have an active exam session. Complete or abandon it first.");

        var questions = await db.Questions
            .AsNoTracking()
            .Include(q => q.AnswerOptions)
            .Where(q => q.TicketNumber == request.TicketNumber && q.IsActive)
            .OrderBy(q => q.Id)
            .ToListAsync(ct);

        if (questions.Count == 0)
            return ApiResponse<ExamSessionDto>.Fail("TICKET_NOT_FOUND", $"Ticket {request.TicketNumber} not found.");

        var expiresAt = now.AddMinutes(TicketTimeLimitMinutes);
        var session = new ExamSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Status = ExamStatus.InProgress,
            Mode = ExamMode.Ticket,
            LicenseCategory = LicenseCategory.AB,
            TicketNumber = request.TicketNumber,
            ExpiresAt = expiresAt,
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

        // Batch presigned URL generation — single parallel call instead of N+1
        var allImageKeys = new List<string>();
        foreach (var q in questions)
        {
            if (q.ImageUrl is not null) allImageKeys.Add(q.ImageUrl);
            foreach (var a in q.AnswerOptions)
                if (a.ImageUrl is not null) allImageKeys.Add(a.ImageUrl);
        }
        var urlMap = await storage.GetPresignedUrlsBatchAsync(allImageKeys, ct);

        var questionDtos = questions.Select((q, idx) =>
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

        logger.LogInformation("Ticket exam started: session {SessionId}, ticket {Ticket}", session.Id, request.TicketNumber);
        return ApiResponse<ExamSessionDto>.Ok(new ExamSessionDto(
            session.Id,
            "inProgress",
            questions.Count,
            (int)Math.Ceiling((double)questions.Count * 80 / 100),
            TicketTimeLimitMinutes,
            expiresAt,
            "ticket",
            request.TicketNumber,
            questionDtos));
    }
}
