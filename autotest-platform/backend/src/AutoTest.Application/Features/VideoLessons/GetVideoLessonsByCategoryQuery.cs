using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.VideoLessons;

public record GetVideoLessonsByCategoryQuery(Guid VideoCategoryId) : IRequest<ApiResponse<List<VideoLessonDto>>>;

public class GetVideoLessonsByCategoryQueryValidator : AbstractValidator<GetVideoLessonsByCategoryQuery>
{
    public GetVideoLessonsByCategoryQueryValidator()
    {
        RuleFor(x => x.VideoCategoryId).NotEmpty();
    }
}

public class GetVideoLessonsByCategoryQueryHandler(
    IApplicationDbContext db,
    IFileStorageService storage,
    ICurrentUser currentUser) : IRequestHandler<GetVideoLessonsByCategoryQuery, ApiResponse<List<VideoLessonDto>>>
{
    public async Task<ApiResponse<List<VideoLessonDto>>> Handle(GetVideoLessonsByCategoryQuery request, CancellationToken ct)
    {
        var userId = currentUser.UserId;

        var lessons = await db.VideoLessons
            .AsNoTracking()
            .Where(l => l.VideoCategoryId == request.VideoCategoryId && l.IsActive)
            .OrderBy(l => l.SortOrder)
            .Select(l => new
            {
                l.Id, l.Title, l.Description, l.SourceType,
                l.ThumbnailUrl, l.DurationSeconds, l.IsFree, l.IsDownloadable, l.LinkedCategoryId,
                Progress = userId.HasValue
                    ? db.UserLessonProgress
                        .Where(p => p.VideoLessonId == l.Id && p.UserId == userId.Value)
                        .Select(p => new { p.IsCompleted, p.WatchedSeconds })
                        .FirstOrDefault()
                    : null
            })
            .ToListAsync(ct);

        var thumbKeys = lessons
            .Where(l => !string.IsNullOrEmpty(l.ThumbnailUrl))
            .Select(l => l.ThumbnailUrl!)
            .ToList();

        var presignedUrls = thumbKeys.Count > 0
            ? await storage.GetPresignedUrlsBatchAsync(thumbKeys, ct)
            : new Dictionary<string, string>();

        var dtos = lessons.Select(l => new VideoLessonDto(
            l.Id, l.Title, l.Description, l.SourceType,
            !string.IsNullOrEmpty(l.ThumbnailUrl) && presignedUrls.TryGetValue(l.ThumbnailUrl!, out var url) ? url : null,
            l.DurationSeconds, l.IsFree, l.IsDownloadable, l.LinkedCategoryId,
            l.Progress?.IsCompleted ?? false,
            l.Progress?.WatchedSeconds ?? 0)).ToList();

        return ApiResponse<List<VideoLessonDto>>.Ok(dtos);
    }
}
