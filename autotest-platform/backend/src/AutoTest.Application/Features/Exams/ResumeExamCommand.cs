using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Exams;

public record ResumeExamCommand(Guid SessionId) : IRequest<ApiResponse<ResumeExamResultDto>>;

public record ResumeExamResultDto(DateTimeOffset? ExpiresAt);

public class ResumeExamCommandValidator : AbstractValidator<ResumeExamCommand>
{
    public ResumeExamCommandValidator() => RuleFor(x => x.SessionId).NotEmpty();
}

public class ResumeExamCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider dateTime,
    ILogger<ResumeExamCommandHandler> logger) : IRequestHandler<ResumeExamCommand, ApiResponse<ResumeExamResultDto>>
{
    public async Task<ApiResponse<ResumeExamResultDto>> Handle(ResumeExamCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return ApiResponse<ResumeExamResultDto>.Fail("UNAUTHORIZED", "Not authenticated.");

        var session = await db.ExamSessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId
                && s.UserId == currentUser.UserId, ct);

        if (session is null)
            return ApiResponse<ResumeExamResultDto>.Fail("SESSION_NOT_FOUND", "Session not found.");

        if (session.Status != ExamStatus.Paused)
            return ApiResponse<ResumeExamResultDto>.Fail("SESSION_NOT_PAUSED", "Only paused sessions can be resumed.");

        var now = dateTime.UtcNow;

        // Restore the timer for timed modes; marafon stays without timer.
        DateTimeOffset? newExpiresAt = session.Mode == ExamMode.Marathon || session.RemainingSecondsAtPause is null
            ? null
            : now.AddSeconds(session.RemainingSecondsAtPause.Value);

        session.Status = ExamStatus.InProgress;
        session.ExpiresAt = newExpiresAt;
        session.PausedAt = null;
        session.RemainingSecondsAtPause = null;
        session.UpdatedAt = now;

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Exam resumed: session {SessionId} mode={Mode} newExpiresAt={ExpiresAt}",
            session.Id, session.Mode, newExpiresAt);

        return ApiResponse<ResumeExamResultDto>.Ok(new ResumeExamResultDto(newExpiresAt));
    }
}
