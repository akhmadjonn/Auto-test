using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;

namespace AutoTest.Application.Features.VideoLessons;

// Public DTOs
public record VideoCategoryDto(
    Guid Id,
    LocalizedText Name,
    LocalizedText? Description,
    string? IconUrl,
    int LessonCount,
    int CompletedCount);

public record VideoLessonDto(
    Guid Id,
    LocalizedText Title,
    LocalizedText? Description,
    VideoSourceType SourceType,
    string? ThumbnailUrl,
    int DurationSeconds,
    bool IsFree,
    bool IsDownloadable,
    Guid? LinkedCategoryId,
    bool IsCompleted,
    int WatchedSeconds);

public record VideoLessonDetailDto(
    Guid Id,
    LocalizedText Title,
    LocalizedText? Description,
    VideoSourceType SourceType,
    string VideoUrl,
    string? ThumbnailUrl,
    int DurationSeconds,
    bool IsFree,
    bool IsDownloadable,
    Guid? LinkedCategoryId,
    bool IsCompleted,
    int WatchedSeconds,
    List<LessonAttachmentDto> Attachments);

public record LessonAttachmentDto(Guid Id, string FileName, string FileUrl, long FileSizeBytes);

// Admin DTOs
public record VideoCategoryAdminDto(
    Guid Id,
    LocalizedText Name,
    LocalizedText? Description,
    string? IconUrl,
    int SortOrder,
    bool IsActive,
    int LessonCount,
    DateTimeOffset CreatedAt);

public record VideoLessonAdminDto(
    Guid Id,
    Guid VideoCategoryId,
    LocalizedText Title,
    LocalizedText? Description,
    VideoSourceType SourceType,
    string? VideoUrl,
    string? ThumbnailUrl,
    int DurationSeconds,
    int SortOrder,
    bool IsFree,
    bool IsDownloadable,
    Guid? LinkedCategoryId,
    bool IsActive,
    DateTimeOffset CreatedAt);
