using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.RoadSigns;

public record RoadSignDto(
    Guid Id,
    string SignCode,
    LocalizedText Name,
    LocalizedText? Description,
    string? ImageUrl,
    string? ThumbnailUrl,
    int SortOrder,
    bool IsActive,
    Guid CategoryId,
    string? CategoryCode);

public record GetRoadSignsByCategoryQuery(Guid? CategoryId = null, bool IncludeInactive = false) : IRequest<ApiResponse<List<RoadSignDto>>>;

public class GetRoadSignsByCategoryQueryHandler(
    IApplicationDbContext db,
    IFileStorageService storage,
    ICacheService cache,
    ILogger<GetRoadSignsByCategoryQueryHandler> logger) : IRequestHandler<GetRoadSignsByCategoryQuery, ApiResponse<List<RoadSignDto>>>
{
    public async Task<ApiResponse<List<RoadSignDto>>> Handle(GetRoadSignsByCategoryQuery request, CancellationToken ct)
    {
        var cacheKey = request.CategoryId.HasValue
            ? $"avtolider:road-signs:category:{request.CategoryId}"
            : "avtolider:road-signs:all";

        if (!request.IncludeInactive)
        {
            var cached = await cache.GetAsync<List<RoadSignDto>>(cacheKey, ct);
            if (cached is not null)
                return ApiResponse<List<RoadSignDto>>.Ok(cached);
        }

        var query = db.RoadSigns
            .AsNoTracking()
            .Include(s => s.Category)
            .AsQueryable();

        if (request.CategoryId.HasValue)
            query = query.Where(s => s.CategoryId == request.CategoryId.Value);

        if (!request.IncludeInactive)
            query = query.Where(s => s.IsActive);

        var signs = await query
            .OrderBy(s => s.Category.SortOrder)
            .ThenBy(s => s.SortOrder)
            .ToListAsync(ct);

        var imageKeys = signs
            .SelectMany(s => new[] { s.ImageUrl, s.ThumbnailUrl })
            .Where(k => !string.IsNullOrEmpty(k))
            .Distinct()
            .ToList();

        var presignedUrls = imageKeys.Count > 0
            ? await storage.GetPresignedUrlsBatchAsync(imageKeys!, ct)
            : new Dictionary<string, string>();

        var dtos = signs.Select(s => new RoadSignDto(
            s.Id,
            s.SignCode,
            s.Name,
            s.Description,
            !string.IsNullOrEmpty(s.ImageUrl) && presignedUrls.TryGetValue(s.ImageUrl, out var imgUrl) ? imgUrl : null,
            !string.IsNullOrEmpty(s.ThumbnailUrl) && presignedUrls.TryGetValue(s.ThumbnailUrl, out var thumbUrl) ? thumbUrl : null,
            s.SortOrder,
            s.IsActive,
            s.CategoryId,
            s.Category?.Code)).ToList();

        if (!request.IncludeInactive)
            await cache.SetAsync(cacheKey, dtos, TimeSpan.FromMinutes(50), ct);

        logger.LogDebug("Road signs loaded from DB: {Count} signs", dtos.Count);
        return ApiResponse<List<RoadSignDto>>.Ok(dtos);
    }
}
