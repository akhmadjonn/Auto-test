using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Exams;

public record PauseExamCommand(Guid SessionId) : IRequest<ApiResponse<PauseExamResultDto>>;

public record PauseExamResultDto(int? RemainingSecondsAtPause, DateTimeOffset PausedAt);

public class PauseExamCommandValidator : AbstractValidator<PauseExamCommand>
{
    public PauseExamCommandValidator() => RuleFor(x => x.SessionId).NotEmpty();
}

public class PauseExamCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider dateTime,
    ILogger<PauseExamCommandHandler> logger) : IRequestHandler<PauseExamCommand, ApiResponse<PauseExamResultDto>>
{
    public async Task<ApiResponse<PauseExamResultDto>> Handle(PauseExamCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return ApiResponse<PauseExamResultDto>.Fail("UNAUTHORIZED", "Not authenticated.");

        var session = await db.ExamSessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId
                && s.UserId == currentUser.UserId, ct);

        if (session is null)
            return ApiResponse<PauseExamResultDto>.Fail("SESSION_NOT_FOUND", "Session not found.");

        if (session.Status != ExamStatus.InProgress)
            return ApiResponse<PauseExamResultDto>.Fail("SESSION_NOT_ACTIVE", "Only in-progress sessions can be paused.");

        var now = dateTime.UtcNow;

        // If timer already expired, fast-forward to Expired instead of pausing.
        if (session.ExpiresAt is { } expiresAt && expiresAt <= now && session.Mode != ExamMode.Marathon)
        {
            session.Status = ExamStatus.Expired;
            session.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
            return ApiResponse<PauseExamResultDto>.Fail("SESSION_EXPIRED", "Session has already expired.");
        }

        // Compute remaining seconds for timed modes; null for marafon (never had a timer).
        int? remaining = session.Mode == ExamMode.Marathon || session.ExpiresAt is null
            ? null
            : Math.Max(0, (int)Math.Floor((session.ExpiresAt.Value - now).TotalSeconds));

        session.Status = ExamStatus.Paused;
        session.PausedAt = now;
        session.RemainingSecondsAtPause = remaining;
        session.ExpiresAt = null;
        session.UpdatedAt = now;

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Exam paused: session {SessionId} mode={Mode} remaining={Remaining}s",
            session.Id, session.Mode, remaining);

        return ApiResponse<PauseExamResultDto>.Ok(new PauseExamResultDto(remaining, now));
    }
}
