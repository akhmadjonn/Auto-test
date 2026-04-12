using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;
using AutoTest.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.VideoLessons;

public record CreateVideoLessonCommand(
    Guid VideoCategoryId,
    string TitleUz,
    string TitleUzLatin,
    string TitleRu,
    string? DescriptionUz,
    string? DescriptionUzLatin,
    string? DescriptionRu,
    VideoSourceType SourceType,
    string VideoUrl,
    string? ThumbnailUrl,
    int DurationSeconds,
    int SortOrder,
    bool IsFree,
    bool IsDownloadable) : IRequest<ApiResponse<Guid>>;

public class CreateVideoLessonCommandValidator : AbstractValidator<CreateVideoLessonCommand>
{
    public CreateVideoLessonCommandValidator()
    {
        RuleFor(x => x.VideoCategoryId).NotEmpty();
        RuleFor(x => x.TitleUz).NotEmpty().MaximumLength(500);
        RuleFor(x => x.TitleUzLatin).NotEmpty().MaximumLength(500);
        RuleFor(x => x.TitleRu).NotEmpty().MaximumLength(500);
        RuleFor(x => x.VideoUrl).MaximumLength(1000)
            .NotEmpty().When(x => x.SourceType != Domain.Common.Enums.VideoSourceType.Upload)
            .WithMessage("Video URL is required for YouTube and External Link sources.");
        RuleFor(x => x.DurationSeconds).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SourceType).IsInEnum();
    }
}

public class CreateVideoLessonCommandHandler(
    IApplicationDbContext db,
    IDateTimeProvider dateTime,
    ICacheService cache,
    ILogger<CreateVideoLessonCommandHandler> logger) : IRequestHandler<CreateVideoLessonCommand, ApiResponse<Guid>>
{
    public async Task<ApiResponse<Guid>> Handle(CreateVideoLessonCommand request, CancellationToken ct)
    {
        var categoryExists = await db.VideoCategories.AnyAsync(c => c.Id == request.VideoCategoryId, ct);
        if (!categoryExists)
            return ApiResponse<Guid>.Fail("CATEGORY_NOT_FOUND", "Video category not found.");

        var description = request.DescriptionUz is not null && request.DescriptionUzLatin is not null && request.DescriptionRu is not null
            ? new LocalizedText(request.DescriptionUz, request.DescriptionUzLatin, request.DescriptionRu)
            : null;

        // Auto-generate YouTube thumbnail if not provided
        var thumbnailUrl = request.ThumbnailUrl;
        if (string.IsNullOrEmpty(thumbnailUrl) && request.SourceType == Domain.Common.Enums.VideoSourceType.YouTube
            && !string.IsNullOrEmpty(request.VideoUrl))
        {
            var videoId = ExtractYouTubeId(request.VideoUrl);
            if (videoId is not null)
                thumbnailUrl = $"https://img.youtube.com/vi/{videoId}/hqdefault.jpg";
        }

        var now = dateTime.UtcNow;
        var lesson = new VideoLesson
        {
            Id = Guid.NewGuid(),
            VideoCategoryId = request.VideoCategoryId,
            Title = new LocalizedText(request.TitleUz, request.TitleUzLatin, request.TitleRu),
            Description = description,
            SourceType = request.SourceType,
            VideoUrl = request.VideoUrl,
            ThumbnailUrl = thumbnailUrl,
            DurationSeconds = request.DurationSeconds,
            SortOrder = request.SortOrder,
            IsFree = request.IsFree,
            IsDownloadable = request.IsDownloadable,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.VideoLessons.Add(lesson);
        await db.SaveChangesAsync(ct);
        await CreateVideoCategoryCommandHandler.InvalidateVideoCategoryCacheAsync(cache, ct);

        logger.LogInformation("Video lesson created: {Id} in category {CategoryId}", lesson.Id, request.VideoCategoryId);
        return ApiResponse<Guid>.Ok(lesson.Id);
    }

    internal static string? ExtractYouTubeId(string url)
    {
        var patterns = new[]
        {
            @"(?:youtube\.com\/watch\?v=|youtu\.be\/|youtube\.com\/embed\/)([a-zA-Z0-9_-]{11})",
            @"youtube\.com\/shorts\/([a-zA-Z0-9_-]{11})"
        };
        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(url, pattern);
            if (match.Success) return match.Groups[1].Value;
        }
        return null;
    }
}
