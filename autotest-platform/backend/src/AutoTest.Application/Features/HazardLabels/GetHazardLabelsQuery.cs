using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.HazardLabels;

public record HazardLabelDto(
    Guid Id,
    string Slug,
    LocalizedText Name,
    LocalizedText Description,
    string? ImageUrl,
    string HazardClass,
    int SortOrder);

public record GetHazardLabelsQuery : IRequest<ApiResponse<List<HazardLabelDto>>>;

public class GetHazardLabelsQueryHandler(
    IApplicationDbContext db,
    IFileStorageService storage,
    ICacheService cache,
    ILogger<GetHazardLabelsQueryHandler> logger) : IRequestHandler<GetHazardLabelsQuery, ApiResponse<List<HazardLabelDto>>>
{
    public async Task<ApiResponse<List<HazardLabelDto>>> Handle(GetHazardLabelsQuery request, CancellationToken ct)
    {
        const string cacheKey = "avtolider:hazard-labels:all";
        var cached = await cache.GetAsync<List<HazardLabelDto>>(cacheKey, ct);
        if (cached is not null)
            return ApiResponse<List<HazardLabelDto>>.Ok(cached);

        var labels = await db.HazardLabels
            .AsNoTracking()
            .OrderBy(l => l.SortOrder)
            .ToListAsync(ct);

        var dtos = new List<HazardLabelDto>(labels.Count);
        foreach (var label in labels)
        {
            string? imageUrl = null;
            if (!string.IsNullOrEmpty(label.ImageUrl))
                imageUrl = await storage.GetPresignedUrlAsync(label.ImageUrl, ct);

            dtos.Add(new HazardLabelDto(
                label.Id,
                label.Slug,
                label.Name,
                label.Description,
                imageUrl,
                label.HazardClass,
                label.SortOrder));
        }

        await cache.SetAsync(cacheKey, dtos, TimeSpan.FromHours(24), ct);
        logger.LogDebug("Hazard labels loaded from DB, cached for 24h");

        return ApiResponse<List<HazardLabelDto>>.Ok(dtos);
    }
}
