using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.VideoLessons;

public record DeleteLessonAttachmentCommand(Guid LessonId, Guid AttachmentId) : IRequest<ApiResponse>;

public class DeleteLessonAttachmentCommandValidator : AbstractValidator<DeleteLessonAttachmentCommand>
{
    public DeleteLessonAttachmentCommandValidator()
    {
        RuleFor(x => x.LessonId).NotEmpty();
        RuleFor(x => x.AttachmentId).NotEmpty();
    }
}

public class DeleteLessonAttachmentCommandHandler(
    IApplicationDbContext db,
    IFileStorageService storage,
    ICacheService cache,
    ILogger<DeleteLessonAttachmentCommandHandler> logger) : IRequestHandler<DeleteLessonAttachmentCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(DeleteLessonAttachmentCommand request, CancellationToken ct)
    {
        var attachment = await db.LessonAttachments.FindAsync([request.AttachmentId], ct);
        if (attachment is null || attachment.VideoLessonId != request.LessonId)
            return ApiResponse.Fail("NOT_FOUND", "Attachment not found.");

        var fileUrl = attachment.FileUrl;
        db.LessonAttachments.Remove(attachment);
        await db.SaveChangesAsync(ct);

        await storage.DeleteAsync(fileUrl, ct);
        await cache.RemoveAsync($"avtolider:video-lesson:{request.LessonId}", ct);

        logger.LogInformation("Attachment deleted: {AttachmentId} from lesson {LessonId}", request.AttachmentId, request.LessonId);
        return ApiResponse.Ok();
    }
}
