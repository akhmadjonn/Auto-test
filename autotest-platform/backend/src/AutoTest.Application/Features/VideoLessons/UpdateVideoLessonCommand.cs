using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.VideoLessons;

public record UpdateVideoLessonCommand(
    Guid Id,
    LocalizedText Title,
    LocalizedText? Description,
    VideoSourceType SourceType,
    string VideoUrl,
    string? ThumbnailUrl,
    int DurationSeconds,
    int SortOrder,
    bool IsFree,
    bool IsDownloadable,
    Guid? LinkedCategoryId,
    bool IsActive) : IRequest<ApiResponse>;

public class UpdateVideoLessonCommandValidator : AbstractValidator<UpdateVideoLessonCommand>
{
    public UpdateVideoLessonCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Title).NotNull();
        RuleFor(x => x.Title.Uz).NotEmpty().MaximumLength(500).When(x => x.Title is not null);
        RuleFor(x => x.Title.UzLatin).NotEmpty().MaximumLength(500).When(x => x.Title is not null);
        RuleFor(x => x.Title.Ru).NotEmpty().MaximumLength(500).When(x => x.Title is not null);
        RuleFor(x => x.VideoUrl).MaximumLength(1000)
            .NotEmpty().When(x => x.SourceType != Domain.Common.Enums.VideoSourceType.Upload)
            .WithMessage("Video URL is required for YouTube and External Link sources.");
        RuleFor(x => x.DurationSeconds).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SourceType).IsInEnum();
    }
}

public class UpdateVideoLessonCommandHandler(
    IApplicationDbContext db,
    IDateTimeProvider dateTime,
    ICacheService cache,
    ILogger<UpdateVideoLessonCommandHandler> logger) : IRequestHandler<UpdateVideoLessonCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(UpdateVideoLessonCommand request, CancellationToken ct)
    {
        var lesson = await db.VideoLessons.FindAsync([request.Id], ct);
        if (lesson is null)
            return ApiResponse.Fail("NOT_FOUND", "Video lesson not found.");

        lesson.Title = request.Title;
        lesson.Description = request.Description;
        lesson.SourceType = request.SourceType;
        lesson.VideoUrl = request.VideoUrl;
        lesson.ThumbnailUrl = request.ThumbnailUrl;
        lesson.DurationSeconds = request.DurationSeconds;
        lesson.SortOrder = request.SortOrder;
        lesson.IsFree = request.IsFree;
        lesson.IsDownloadable = request.IsDownloadable;
        lesson.LinkedCategoryId = request.LinkedCategoryId;
        lesson.IsActive = request.IsActive;
        lesson.UpdatedAt = dateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        await CreateVideoCategoryCommandHandler.InvalidateVideoCategoryCacheAsync(cache, ct);
        await cache.RemoveAsync($"avtolider:video-lesson:{request.Id}", ct);

        logger.LogInformation("Video lesson updated: {Id}", request.Id);
        return ApiResponse.Ok();
    }
}
