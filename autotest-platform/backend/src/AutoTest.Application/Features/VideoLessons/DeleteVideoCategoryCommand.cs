using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.VideoLessons;

public record DeleteVideoCategoryCommand(Guid Id) : IRequest<ApiResponse>;

public class DeleteVideoCategoryCommandValidator : AbstractValidator<DeleteVideoCategoryCommand>
{
    public DeleteVideoCategoryCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class DeleteVideoCategoryCommandHandler(
    IApplicationDbContext db,
    ICacheService cache,
    ILogger<DeleteVideoCategoryCommandHandler> logger) : IRequestHandler<DeleteVideoCategoryCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(DeleteVideoCategoryCommand request, CancellationToken ct)
    {
        var category = await db.VideoCategories.FindAsync([request.Id], ct);
        if (category is null)
            return ApiResponse.Fail("NOT_FOUND", "Video category not found.");

        var hasLessons = await db.VideoLessons.AnyAsync(l => l.VideoCategoryId == request.Id, ct);
        if (hasLessons)
            return ApiResponse.Fail("HAS_LESSONS", "Cannot delete category that has lessons. Remove all lessons first.");

        db.VideoCategories.Remove(category);
        await db.SaveChangesAsync(ct);
        await CreateVideoCategoryCommandHandler.InvalidateVideoCategoryCacheAsync(cache, ct);

        logger.LogInformation("Video category deleted: {Id}", request.Id);
        return ApiResponse.Ok();
    }
}
