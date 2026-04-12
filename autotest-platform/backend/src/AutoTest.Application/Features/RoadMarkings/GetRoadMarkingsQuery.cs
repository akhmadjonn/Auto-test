using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.RoadMarkings;

public record RoadMarkingDto(
    Guid Id,
    string MarkingCode,
    RoadMarkingType MarkingType,
    LocalizedText Name,
    LocalizedText? Description,
    string? ImageUrl,
    string? ThumbnailUrl,
    int SortOrder,
    bool IsActive);

public record GetRoadMarkingsQuery(RoadMarkingType? Type = null, bool IncludeInactive = false) : IRequest<ApiResponse<List<RoadMarkingDto>>>;

public class GetRoadMarkingsQueryHandler(
    IApplicationDbContext db,
    IFileStorageService storage,
    ICacheService cache,
    ILogger<GetRoadMarkingsQueryHandler> logger) : IRequestHandler<GetRoadMarkingsQuery, ApiResponse<List<RoadMarkingDto>>>
{
    public async Task<ApiResponse<List<RoadMarkingDto>>> Handle(GetRoadMarkingsQuery request, CancellationToken ct)
    {
        var cacheKey = request.Type.HasValue
            ? $"avtolider:road-markings:{request.Type.Value.ToString().ToLowerInvariant()}"
            : "avtolider:road-markings:all";

        if (!request.IncludeInactive)
        {
            var cached = await cache.GetAsync<List<RoadMarkingDto>>(cacheKey, ct);
            if (cached is not null)
                return ApiResponse<List<RoadMarkingDto>>.Ok(cached);
        }

        var query = db.RoadMarkings.AsNoTracking().AsQueryable();

        if (request.Type.HasValue)
            query = query.Where(m => m.MarkingType == request.Type.Value);

        if (!request.IncludeInactive)
            query = query.Where(m => m.IsActive);

        var markings = await query
            .OrderBy(m => m.MarkingType)
            .ThenBy(m => m.SortOrder)
            .ToListAsync(ct);

        var imageKeys = markings
            .SelectMany(m => new[] { m.ImageUrl, m.ThumbnailUrl })
            .Where(k => !string.IsNullOrEmpty(k))
            .Distinct()
            .ToList();

        var presignedUrls = imageKeys.Count > 0
            ? await storage.GetPresignedUrlsBatchAsync(imageKeys!, ct)
            : new Dictionary<string, string>();

        var dtos = markings.Select(m => new RoadMarkingDto(
            m.Id, m.MarkingCode, m.MarkingType, m.Name, m.Description,
            !string.IsNullOrEmpty(m.ImageUrl) && presignedUrls.TryGetValue(m.ImageUrl, out var imgUrl) ? imgUrl : null,
            !string.IsNullOrEmpty(m.ThumbnailUrl) && presignedUrls.TryGetValue(m.ThumbnailUrl, out var thumbUrl) ? thumbUrl : null,
            m.SortOrder, m.IsActive)).ToList();

        if (!request.IncludeInactive)
            await cache.SetAsync(cacheKey, dtos, TimeSpan.FromMinutes(50), ct);

        logger.LogDebug("Road markings loaded from DB: {Count} markings", dtos.Count);
        return ApiResponse<List<RoadMarkingDto>>.Ok(dtos);
    }
}
