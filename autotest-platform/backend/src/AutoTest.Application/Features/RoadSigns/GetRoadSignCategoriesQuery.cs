using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.RoadSigns;

public record RoadSignCategoryDto(
    Guid Id,
    string Slug,
    string Code,
    LocalizedText Name,
    LocalizedText Description,
    string? IconUrl,
    int SortOrder,
    bool IsActive,
    int SignCount);

public record GetRoadSignCategoriesQuery(bool IncludeInactive = false) : IRequest<ApiResponse<List<RoadSignCategoryDto>>>;

public class GetRoadSignCategoriesQueryHandler(
    IApplicationDbContext db,
    IFileStorageService storage,
    ICacheService cache,
    ILogger<GetRoadSignCategoriesQueryHandler> logger) : IRequestHandler<GetRoadSignCategoriesQuery, ApiResponse<List<RoadSignCategoryDto>>>
{
    public async Task<ApiResponse<List<RoadSignCategoryDto>>> Handle(GetRoadSignCategoriesQuery request, CancellationToken ct)
    {
        var cacheKey = request.IncludeInactive
            ? "avtolider:road-sign-categories:all-with-inactive"
            : "avtolider:road-sign-categories:all";

        var cached = await cache.GetAsync<List<RoadSignCategoryDto>>(cacheKey, ct);
        if (cached is not null)
            return ApiResponse<List<RoadSignCategoryDto>>.Ok(cached);

        var query = db.RoadSignCategories.AsNoTracking();
        if (!request.IncludeInactive)
            query = query.Where(c => c.IsActive);

        var categories = await query
            .OrderBy(c => c.SortOrder)
            .Select(c => new
            {
                c.Id, c.Slug, c.Code, c.Name, c.Description,
                c.IconUrl, c.SortOrder, c.IsActive,
                SignCount = c.Signs.Count(s => s.IsActive)
            })
            .ToListAsync(ct);

        var iconKeys = categories
            .Where(c => !string.IsNullOrEmpty(c.IconUrl))
            .Select(c => c.IconUrl!)
            .ToList();

        var presignedUrls = iconKeys.Count > 0
            ? await storage.GetPresignedUrlsBatchAsync(iconKeys, ct)
            : new Dictionary<string, string>();

        var dtos = categories.Select(c => new RoadSignCategoryDto(
            c.Id, c.Slug, c.Code, c.Name, c.Description,
            !string.IsNullOrEmpty(c.IconUrl) && presignedUrls.TryGetValue(c.IconUrl!, out var url) ? url : null,
            c.SortOrder, c.IsActive, c.SignCount)).ToList();

        await cache.SetAsync(cacheKey, dtos, TimeSpan.FromMinutes(50), ct);
        logger.LogDebug("Road sign categories loaded from DB, cached for 50min");

        return ApiResponse<List<RoadSignCategoryDto>>.Ok(dtos);
    }
}
