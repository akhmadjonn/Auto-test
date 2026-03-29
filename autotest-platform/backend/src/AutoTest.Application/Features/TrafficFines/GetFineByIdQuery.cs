using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.TrafficFines;

public record GetFineByIdQuery(Guid Id) : IRequest<ApiResponse<FineDto>>;

public class GetFineByIdQueryHandler(
    IApplicationDbContext db,
    ICacheService cache,
    IFileStorageService storage,
    ILogger<GetFineByIdQueryHandler> logger) : IRequestHandler<GetFineByIdQuery, ApiResponse<FineDto>>
{
    public async Task<ApiResponse<FineDto>> Handle(GetFineByIdQuery request, CancellationToken ct)
    {
        var cacheKey = $"avtolider:fines:{request.Id}";
        var cached = await cache.GetAsync<FineDto>(cacheKey, ct);
        if (cached is not null)
            return ApiResponse<FineDto>.Ok(cached);

        var fine = await db.TrafficFines.FindAsync([request.Id], ct);
        if (fine is null)
            return ApiResponse<FineDto>.Fail("NOT_FOUND", "Traffic fine not found.");

        string? presignedUrl = null;
        if (fine.ImageUrl is not null)
            presignedUrl = await storage.GetPresignedUrlAsync(fine.ImageUrl, ct);

        var dto = new FineDto(
            fine.Id,
            fine.ArticleNumber,
            fine.ViolationDescription,
            fine.AdditionalNotes,
            fine.PenaltyAmountTiyins,
            fine.PenaltyMaxTiyins,
            presignedUrl,
            fine.SortOrder,
            fine.IsActive);

        await cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(50), ct);
        logger.LogDebug("Fine {FineId} loaded from DB, cached for 50min", request.Id);

        return ApiResponse<FineDto>.Ok(dto);
    }
}
