using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.VideoLessons;

public record GetVideoLessonByIdQuery(Guid Id) : IRequest<ApiResponse<VideoLessonDetailDto>>;

public class GetVideoLessonByIdQueryValidator : AbstractValidator<GetVideoLessonByIdQuery>
{
    public GetVideoLessonByIdQueryValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class GetVideoLessonByIdQueryHandler(
    IApplicationDbContext db,
    IFileStorageService storage,
    ICurrentUser currentUser,
    IDateTimeProvider dateTime,
    ICacheService cache) : IRequestHandler<GetVideoLessonByIdQuery, ApiResponse<VideoLessonDetailDto>>
{
    public async Task<ApiResponse<VideoLessonDetailDto>> Handle(GetVideoLessonByIdQuery request, CancellationToken ct)
    {
        var lesson = await db.VideoLessons
            .AsNoTracking()
            .Include(l => l.Attachments.OrderBy(a => a.SortOrder))
            .FirstOrDefaultAsync(l => l.Id == request.Id && l.IsActive, ct);

        if (lesson is null)
            return ApiResponse<VideoLessonDetailDto>.Fail("NOT_FOUND", "Video lesson not found.");

        // subscription check for premium lessons
        if (!lesson.IsFree)
        {
            var userId = currentUser.UserId;
            if (!userId.HasValue)
                return ApiResponse<VideoLessonDetailDto>.Fail("PREMIUM_REQUIRED", "This lesson requires an active subscription.");

            var now = dateTime.UtcNow;
            var hasSubscription = await db.Subscriptions
                .AnyAsync(s => s.UserId == userId.Value && s.Status == SubscriptionStatus.Active && s.ExpiresAt > now, ct);

            if (!hasSubscription)
                return ApiResponse<VideoLessonDetailDto>.Fail("PREMIUM_REQUIRED", "This lesson requires an active subscription.");
        }

        // resolve video URL
        var videoUrl = lesson.SourceType switch
        {
            VideoSourceType.Upload or VideoSourceType.Presentation => await storage.GetPresignedUrlAsync(lesson.VideoUrl, ct),
            _ => lesson.VideoUrl
        };

        // resolve thumbnail URL
        string? thumbnailUrl = null;
        if (!string.IsNullOrEmpty(lesson.ThumbnailUrl))
            thumbnailUrl = await storage.GetPresignedUrlAsync(lesson.ThumbnailUrl, ct);

        // resolve attachment URLs
        var attachmentKeys = lesson.Attachments
            .Select(a => a.FileUrl)
            .ToList();

        var attachmentUrls = attachmentKeys.Count > 0
            ? await storage.GetPresignedUrlsBatchAsync(attachmentKeys, ct)
            : new Dictionary<string, string>();

        var attachmentDtos = lesson.Attachments.Select(a => new LessonAttachmentDto(
            a.Id, a.FileName,
            attachmentUrls.TryGetValue(a.FileUrl, out var aUrl) ? aUrl : a.FileUrl,
            a.FileSizeBytes)).ToList();

        // get user progress
        var userId2 = currentUser.UserId;
        var progress = userId2.HasValue
            ? await db.UserLessonProgress
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.VideoLessonId == request.Id && p.UserId == userId2.Value, ct)
            : null;

        var dto = new VideoLessonDetailDto(
            lesson.Id, lesson.Title, lesson.Description, lesson.SourceType,
            videoUrl, thumbnailUrl, lesson.DurationSeconds, lesson.IsFree, lesson.IsDownloadable, lesson.LinkedCategoryId,
            progress?.IsCompleted ?? false,
            progress?.WatchedSeconds ?? 0,
            attachmentDtos);

        return ApiResponse<VideoLessonDetailDto>.Ok(dto);
    }
}
