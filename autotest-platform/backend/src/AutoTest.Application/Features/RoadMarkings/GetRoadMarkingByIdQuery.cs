using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.RoadMarkings;

public record GetRoadMarkingByIdQuery(Guid Id) : IRequest<ApiResponse<RoadMarkingDto>>;

public class GetRoadMarkingByIdQueryHandler(
    IApplicationDbContext db,
    IFileStorageService storage,
    ICacheService cache,
    ILogger<GetRoadMarkingByIdQueryHandler> logger) : IRequestHandler<GetRoadMarkingByIdQuery, ApiResponse<RoadMarkingDto>>
{
    public async Task<ApiResponse<RoadMarkingDto>> Handle(GetRoadMarkingByIdQuery request, CancellationToken ct)
    {
        var cacheKey = $"avtolider:road-markings:{request.Id}";
        var cached = await cache.GetAsync<RoadMarkingDto>(cacheKey, ct);
        if (cached is not null)
            return ApiResponse<RoadMarkingDto>.Ok(cached);

        var marking = await db.RoadMarkings.FindAsync([request.Id], ct);
        if (marking is null)
            return ApiResponse<RoadMarkingDto>.Fail("NOT_FOUND", "Road marking not found.");

        string? imageUrl = null;
        string? thumbUrl = null;

        if (!string.IsNullOrEmpty(marking.ImageUrl))
            imageUrl = await storage.GetPresignedUrlAsync(marking.ImageUrl, ct);
        if (!string.IsNullOrEmpty(marking.ThumbnailUrl))
            thumbUrl = await storage.GetPresignedUrlAsync(marking.ThumbnailUrl, ct);

        var dto = new RoadMarkingDto(
            marking.Id, marking.MarkingCode, marking.MarkingType,
            marking.Name, marking.Description,
            imageUrl, thumbUrl, marking.SortOrder, marking.IsActive);

        await cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(50), ct);
        return ApiResponse<RoadMarkingDto>.Ok(dto);
    }
}
