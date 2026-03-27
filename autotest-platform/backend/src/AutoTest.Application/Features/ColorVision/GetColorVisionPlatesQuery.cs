using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using MediatR;

namespace AutoTest.Application.Features.ColorVision;

public record GetColorVisionPlatesQuery : IRequest<ApiResponse<List<ColorVisionPlateDto>>>;

public record ColorVisionPlateDto(string PlateId, string ImageUrl);

public class GetColorVisionPlatesQueryHandler(
    IFileStorageService storage,
    ICacheService cache) : IRequestHandler<GetColorVisionPlatesQuery, ApiResponse<List<ColorVisionPlateDto>>>
{
    private const string CacheKey = "avtolider:color-vision:plates";

    public async Task<ApiResponse<List<ColorVisionPlateDto>>> Handle(GetColorVisionPlatesQuery request, CancellationToken ct)
    {
        var cached = await cache.GetAsync<List<ColorVisionPlateDto>>(CacheKey, ct);
        if (cached is not null)
            return ApiResponse<List<ColorVisionPlateDto>>.Ok(cached);

        var plates = new List<ColorVisionPlateDto>(ColorVisionPlates.All.Count);

        foreach (var plate in ColorVisionPlates.All)
        {
            var url = await storage.GetPresignedUrlAsync(plate.ImageKey, ct);
            plates.Add(new ColorVisionPlateDto(plate.PlateId, url));
        }

        await cache.SetAsync(CacheKey, plates, TimeSpan.FromHours(1), ct);

        return ApiResponse<List<ColorVisionPlateDto>>.Ok(plates);
    }
}
