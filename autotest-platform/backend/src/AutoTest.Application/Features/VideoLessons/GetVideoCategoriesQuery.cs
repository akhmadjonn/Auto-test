using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.VideoLessons;

public record GetVideoCategoriesQuery : IRequest<ApiResponse<List<VideoCategoryDto>>>;

public class GetVideoCategoriesQueryHandler(
    IApplicationDbContext db,
    IFileStorageService storage,
    ICurrentUser currentUser,
    ICacheService cache) : IRequestHandler<GetVideoCategoriesQuery, ApiResponse<List<VideoCategoryDto>>>
{
    public async Task<ApiResponse<List<VideoCategoryDto>>> Handle(GetVideoCategoriesQuery request, CancellationToken ct)
    {
        var userId = currentUser.UserId;

        var categories = await db.VideoCategories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .Select(c => new
            {
                c.Id, c.Name, c.Description, c.IconUrl,
                LessonCount = c.Lessons.Count(l => l.IsActive),
                CompletedCount = userId.HasValue
                    ? c.Lessons.Count(l => l.IsActive && db.UserLessonProgress
                        .Any(p => p.VideoLessonId == l.Id && p.UserId == userId.Value && p.IsCompleted))
                    : 0
            })
            .ToListAsync(ct);

        var iconKeys = categories
            .Where(c => !string.IsNullOrEmpty(c.IconUrl))
            .Select(c => c.IconUrl!)
            .ToList();

        var presignedUrls = iconKeys.Count > 0
            ? await storage.GetPresignedUrlsBatchAsync(iconKeys, ct)
            : new Dictionary<string, string>();

        var dtos = categories.Select(c => new VideoCategoryDto(
            c.Id, c.Name, c.Description,
            !string.IsNullOrEmpty(c.IconUrl) && presignedUrls.TryGetValue(c.IconUrl!, out var url) ? url : null,
            c.LessonCount, c.CompletedCount)).ToList();

        return ApiResponse<List<VideoCategoryDto>>.Ok(dtos);
    }
}
