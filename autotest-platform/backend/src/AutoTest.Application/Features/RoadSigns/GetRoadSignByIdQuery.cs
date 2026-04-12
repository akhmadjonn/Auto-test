using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.RoadSigns;

public record GetRoadSignByIdQuery(Guid Id) : IRequest<ApiResponse<RoadSignDto>>;

public class GetRoadSignByIdQueryHandler(
    IApplicationDbContext db,
    IFileStorageService storage,
    ICacheService cache,
    ILogger<GetRoadSignByIdQueryHandler> logger) : IRequestHandler<GetRoadSignByIdQuery, ApiResponse<RoadSignDto>>
{
    public async Task<ApiResponse<RoadSignDto>> Handle(GetRoadSignByIdQuery request, CancellationToken ct)
    {
        var cacheKey = $"avtolider:road-signs:{request.Id}";
        var cached = await cache.GetAsync<RoadSignDto>(cacheKey, ct);
        if (cached is not null)
            return ApiResponse<RoadSignDto>.Ok(cached);

        var sign = await db.RoadSigns
            .AsNoTracking()
            .Include(s => s.Category)
            .FirstOrDefaultAsync(s => s.Id == request.Id, ct);

        if (sign is null)
            return ApiResponse<RoadSignDto>.Fail("NOT_FOUND", "Road sign not found.");

        string? imageUrl = null;
        string? thumbUrl = null;

        if (!string.IsNullOrEmpty(sign.ImageUrl))
            imageUrl = await storage.GetPresignedUrlAsync(sign.ImageUrl, ct);
        if (!string.IsNullOrEmpty(sign.ThumbnailUrl))
            thumbUrl = await storage.GetPresignedUrlAsync(sign.ThumbnailUrl, ct);

        var dto = new RoadSignDto(
            sign.Id, sign.SignCode, sign.Name, sign.Description,
            imageUrl, thumbUrl, sign.SortOrder, sign.IsActive,
            sign.CategoryId, sign.Category?.Code);

        await cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(50), ct);
        return ApiResponse<RoadSignDto>.Ok(dto);
    }
}
