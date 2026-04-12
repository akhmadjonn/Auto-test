using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.VideoLessons;

public record UpdateLessonProgressCommand(Guid VideoLessonId, int WatchedSeconds) : IRequest<ApiResponse>;

public class UpdateLessonProgressCommandValidator : AbstractValidator<UpdateLessonProgressCommand>
{
    public UpdateLessonProgressCommandValidator()
    {
        RuleFor(x => x.VideoLessonId).NotEmpty();
        RuleFor(x => x.WatchedSeconds).GreaterThanOrEqualTo(0);
    }
}

public class UpdateLessonProgressCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider dateTime,
    ILogger<UpdateLessonProgressCommandHandler> logger) : IRequestHandler<UpdateLessonProgressCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(UpdateLessonProgressCommand request, CancellationToken ct)
    {
        var userId = currentUser.UserId;
        if (!userId.HasValue)
            return ApiResponse.Fail("UNAUTHORIZED", "User must be authenticated.");

        var lesson = await db.VideoLessons
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == request.VideoLessonId && l.IsActive, ct);

        if (lesson is null)
            return ApiResponse.Fail("NOT_FOUND", "Video lesson not found.");

        var now = dateTime.UtcNow;
        var progress = await db.UserLessonProgress
            .FirstOrDefaultAsync(p => p.UserId == userId.Value && p.VideoLessonId == request.VideoLessonId, ct);

        if (progress is null)
        {
            progress = new Domain.Entities.UserLessonProgress
            {
                Id = Guid.NewGuid(),
                UserId = userId.Value,
                VideoLessonId = request.VideoLessonId,
                WatchedSeconds = request.WatchedSeconds,
                LastWatchedAt = now,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.UserLessonProgress.Add(progress);
        }
        else
        {
            // only update if new progress is greater
            if (request.WatchedSeconds > progress.WatchedSeconds)
                progress.WatchedSeconds = request.WatchedSeconds;

            progress.LastWatchedAt = now;
            progress.UpdatedAt = now;
        }

        // mark completed if watched >= 90% of duration
        if (!progress.IsCompleted && lesson.DurationSeconds > 0)
        {
            var threshold = (int)(lesson.DurationSeconds * 0.9);
            if (progress.WatchedSeconds >= threshold)
            {
                progress.IsCompleted = true;
                progress.CompletedAt = now;
            }
        }

        await db.SaveChangesAsync(ct);

        logger.LogDebug("Lesson progress updated: User={UserId} Lesson={LessonId} Watched={Seconds}s Completed={Completed}",
            userId.Value, request.VideoLessonId, progress.WatchedSeconds, progress.IsCompleted);

        return ApiResponse.Ok();
    }
}
