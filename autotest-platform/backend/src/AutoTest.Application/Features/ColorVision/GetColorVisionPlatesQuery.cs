using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.ColorVision;

public record GetColorVisionPlatesQuery : IRequest<ApiResponse<List<ColorVisionPlateDto>>>;

public record ColorVisionPlateDto(string PlateId, string ImageUrl);

public class GetColorVisionPlatesQueryHandler(
    IApplicationDbContext db,
    IFileStorageService storage,
    ICacheService cache) : IRequestHandler<GetColorVisionPlatesQuery, ApiResponse<List<ColorVisionPlateDto>>>
{
    private const string CacheKey = "avtolider:color-vision:plates";

    public async Task<ApiResponse<List<ColorVisionPlateDto>>> Handle(GetColorVisionPlatesQuery request, CancellationToken ct)
    {
        var cached = await cache.GetAsync<List<ColorVisionPlateDto>>(CacheKey, ct);
        if (cached is not null)
            return ApiResponse<List<ColorVisionPlateDto>>.Ok(cached);

        var plates = await db.ColorVisionPlates
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder)
            .ToListAsync(ct);

        var dtos = new List<ColorVisionPlateDto>(plates.Count);
        foreach (var plate in plates)
        {
            var url = await storage.GetPresignedUrlAsync(plate.ImageUrl, ct);
            dtos.Add(new ColorVisionPlateDto($"plate-{plate.PlateNumber:D2}", url));
        }

        await cache.SetAsync(CacheKey, dtos, TimeSpan.FromMinutes(50), ct);

        return ApiResponse<List<ColorVisionPlateDto>>.Ok(dtos);
    }
}
