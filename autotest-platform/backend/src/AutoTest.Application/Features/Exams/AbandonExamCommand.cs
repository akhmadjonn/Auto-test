using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.Exams;

public record AbandonExamCommand(Guid SessionId) : IRequest<ApiResponse>;

public class AbandonExamCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider dateTime) : IRequestHandler<AbandonExamCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(AbandonExamCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return ApiResponse.Fail("UNAUTHORIZED", "Not authenticated.");

        var session = await db.ExamSessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId
                && s.UserId == currentUser.UserId
                && s.Status == ExamStatus.InProgress, ct);

        if (session is null)
            return ApiResponse.Fail("SESSION_NOT_FOUND", "Active session not found.");

        session.Status = ExamStatus.Abandoned;
        session.CompletedAt = dateTime.UtcNow;
        session.UpdatedAt = dateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return ApiResponse.Ok();
    }
}
