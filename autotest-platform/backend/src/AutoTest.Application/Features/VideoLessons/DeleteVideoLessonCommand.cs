using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.VideoLessons;

public record DeleteVideoLessonCommand(Guid Id) : IRequest<ApiResponse>;

public class DeleteVideoLessonCommandValidator : AbstractValidator<DeleteVideoLessonCommand>
{
    public DeleteVideoLessonCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class DeleteVideoLessonCommandHandler(
    IApplicationDbContext db,
    IFileStorageService storage,
    ICacheService cache,
    ILogger<DeleteVideoLessonCommandHandler> logger) : IRequestHandler<DeleteVideoLessonCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(DeleteVideoLessonCommand request, CancellationToken ct)
    {
        var lesson = await db.VideoLessons
            .Include(l => l.Attachments)
            .FirstOrDefaultAsync(l => l.Id == request.Id, ct);

        if (lesson is null)
            return ApiResponse.Fail("NOT_FOUND", "Video lesson not found.");

        // collect MinIO keys to delete
        var keysToDelete = new List<string>();
        if (lesson.SourceType == Domain.Common.Enums.VideoSourceType.Upload && !string.IsNullOrEmpty(lesson.VideoUrl))
            keysToDelete.Add(lesson.VideoUrl);
        if (!string.IsNullOrEmpty(lesson.ThumbnailUrl))
            keysToDelete.Add(lesson.ThumbnailUrl);
        foreach (var attachment in lesson.Attachments)
            keysToDelete.Add(attachment.FileUrl);

        db.VideoLessons.Remove(lesson);
        await db.SaveChangesAsync(ct);

        // clean up files from MinIO
        if (keysToDelete.Count > 0)
            await storage.DeleteManyAsync(keysToDelete, ct);

        await CreateVideoCategoryCommandHandler.InvalidateVideoCategoryCacheAsync(cache, ct);
        await cache.RemoveAsync($"avtolider:video-lesson:{request.Id}", ct);

        logger.LogInformation("Video lesson deleted: {Id}", request.Id);
        return ApiResponse.Ok();
    }
}
