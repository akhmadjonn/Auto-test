using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.VideoLessons;

public record UploadLessonAttachmentCommand(
    Guid LessonId,
    Stream FileStream,
    string FileName,
    string ContentType,
    long FileSizeBytes) : IRequest<ApiResponse<LessonAttachmentDto>>;

public class UploadLessonAttachmentCommandValidator : AbstractValidator<UploadLessonAttachmentCommand>
{
    public UploadLessonAttachmentCommandValidator()
    {
        RuleFor(x => x.LessonId).NotEmpty();
        RuleFor(x => x.FileStream).NotNull();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(500);
        RuleFor(x => x.FileSizeBytes).GreaterThan(0);
    }
}

public class UploadLessonAttachmentCommandHandler(
    IApplicationDbContext db,
    IFileStorageService storage,
    IDateTimeProvider dateTime,
    ICacheService cache,
    ILogger<UploadLessonAttachmentCommandHandler> logger) : IRequestHandler<UploadLessonAttachmentCommand, ApiResponse<LessonAttachmentDto>>
{
    public async Task<ApiResponse<LessonAttachmentDto>> Handle(UploadLessonAttachmentCommand request, CancellationToken ct)
    {
        var lessonExists = await db.VideoLessons.AnyAsync(l => l.Id == request.LessonId, ct);
        if (!lessonExists)
            return ApiResponse<LessonAttachmentDto>.Fail("NOT_FOUND", "Video lesson not found.");

        var safeFileName = Path.GetFileName(request.FileName);
        var storageFileName = $"{Guid.NewGuid()}_{safeFileName}";
        var objectKey = await storage.UploadFileAsync(request.FileStream, "lessons/attachments", storageFileName, request.ContentType, ct);

        var maxSort = await db.LessonAttachments
            .Where(a => a.VideoLessonId == request.LessonId)
            .MaxAsync(a => (int?)a.SortOrder, ct) ?? 0;

        var now = dateTime.UtcNow;
        var attachment = new LessonAttachment
        {
            Id = Guid.NewGuid(),
            VideoLessonId = request.LessonId,
            FileName = safeFileName,
            FileUrl = objectKey,
            FileSizeBytes = request.FileSizeBytes,
            SortOrder = maxSort + 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.LessonAttachments.Add(attachment);
        await db.SaveChangesAsync(ct);
        await cache.RemoveAsync($"avtolider:video-lesson:{request.LessonId}", ct);

        var presignedUrl = await storage.GetPresignedUrlAsync(objectKey, ct);

        logger.LogInformation("Attachment uploaded for lesson {LessonId}: {AttachmentId}", request.LessonId, attachment.Id);
        return ApiResponse<LessonAttachmentDto>.Ok(new LessonAttachmentDto(
            attachment.Id, attachment.FileName, presignedUrl, attachment.FileSizeBytes));
    }
}
