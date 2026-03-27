using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.HazardLabels;

public record GetHazardLabelByIdQuery(Guid Id) : IRequest<ApiResponse<HazardLabelDto>>;

public class GetHazardLabelByIdQueryHandler(
    IApplicationDbContext db,
    IFileStorageService storage,
    ICacheService cache,
    ILogger<GetHazardLabelByIdQueryHandler> logger) : IRequestHandler<GetHazardLabelByIdQuery, ApiResponse<HazardLabelDto>>
{
    public async Task<ApiResponse<HazardLabelDto>> Handle(GetHazardLabelByIdQuery request, CancellationToken ct)
    {
        var cacheKey = $"avtolider:hazard-labels:{request.Id}";
        var cached = await cache.GetAsync<HazardLabelDto>(cacheKey, ct);
        if (cached is not null)
            return ApiResponse<HazardLabelDto>.Ok(cached);

        var label = await db.HazardLabels.FindAsync([request.Id], ct);
        if (label is null)
            return ApiResponse<HazardLabelDto>.Fail("NOT_FOUND", "Hazard label not found.");

        string? imageUrl = null;
        if (!string.IsNullOrEmpty(label.ImageUrl))
            imageUrl = await storage.GetPresignedUrlAsync(label.ImageUrl, ct);

        var dto = new HazardLabelDto(
            label.Id,
            label.Slug,
            label.Name,
            label.Description,
            imageUrl,
            label.HazardClass,
            label.SortOrder);

        await cache.SetAsync(cacheKey, dto, TimeSpan.FromHours(24), ct);
        logger.LogDebug("Hazard label {Id} loaded from DB, cached for 24h", request.Id);

        return ApiResponse<HazardLabelDto>.Ok(dto);
    }
}
