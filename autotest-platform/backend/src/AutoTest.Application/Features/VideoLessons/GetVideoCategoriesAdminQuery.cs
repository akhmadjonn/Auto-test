using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.VideoLessons;

public record GetVideoCategoriesAdminQuery : IRequest<ApiResponse<List<VideoCategoryAdminDto>>>;

public class GetVideoCategoriesAdminQueryHandler(
    IApplicationDbContext db,
    IFileStorageService storage,
    ICacheService cache) : IRequestHandler<GetVideoCategoriesAdminQuery, ApiResponse<List<VideoCategoryAdminDto>>>
{
    public async Task<ApiResponse<List<VideoCategoryAdminDto>>> Handle(GetVideoCategoriesAdminQuery request, CancellationToken ct)
    {
        const string cacheKey = "avtolider:video-categories:admin";
        var cached = await cache.GetAsync<List<VideoCategoryAdminDto>>(cacheKey, ct);
        if (cached is not null)
            return ApiResponse<List<VideoCategoryAdminDto>>.Ok(cached);

        var categories = await db.VideoCategories
            .AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .Select(c => new
            {
                c.Id, c.Name, c.Description, c.IconUrl,
                c.SortOrder, c.IsActive, c.CreatedAt,
                LessonCount = c.Lessons.Count
            })
            .ToListAsync(ct);

        var iconKeys = categories
            .Where(c => !string.IsNullOrEmpty(c.IconUrl))
            .Select(c => c.IconUrl!)
            .ToList();

        var presignedUrls = iconKeys.Count > 0
            ? await storage.GetPresignedUrlsBatchAsync(iconKeys, ct)
            : new Dictionary<string, string>();

        var dtos = categories.Select(c => new VideoCategoryAdminDto(
            c.Id, c.Name, c.Description,
            !string.IsNullOrEmpty(c.IconUrl) && presignedUrls.TryGetValue(c.IconUrl!, out var url) ? url : null,
            c.SortOrder, c.IsActive, c.LessonCount, c.CreatedAt)).ToList();

        await cache.SetAsync(cacheKey, dtos, TimeSpan.FromMinutes(10), ct);
        return ApiResponse<List<VideoCategoryAdminDto>>.Ok(dtos);
    }
}
