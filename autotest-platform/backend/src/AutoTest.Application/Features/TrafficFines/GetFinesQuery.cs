using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.TrafficFines;

public record GetFinesQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    long? MinPenalty = null,
    long? MaxPenalty = null) : IRequest<ApiResponse<PaginatedList<FineDto>>>;

public record FineDto(
    Guid Id,
    string ArticleNumber,
    LocalizedText ViolationDescription,
    LocalizedText? AdditionalNotes,
    long PenaltyAmountTiyins,
    long? PenaltyMaxTiyins,
    string? ImageUrl,
    int SortOrder,
    bool IsActive);

public class GetFinesQueryHandler(
    IApplicationDbContext db,
    ICacheService cache,
    IFileStorageService storage,
    ILogger<GetFinesQueryHandler> logger) : IRequestHandler<GetFinesQuery, ApiResponse<PaginatedList<FineDto>>>
{
    public async Task<ApiResponse<PaginatedList<FineDto>>> Handle(GetFinesQuery request, CancellationToken ct)
    {
        var cacheKey = $"avtolider:fines:list:{request.Page}:{request.PageSize}:{request.Search}:{request.MinPenalty}:{request.MaxPenalty}";
        var cached = await cache.GetAsync<PaginatedList<FineDto>>(cacheKey, ct);
        if (cached is not null)
            return ApiResponse<PaginatedList<FineDto>>.Ok(cached);

        var query = db.TrafficFines
            .AsNoTracking()
            .Where(f => f.IsActive);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(f =>
                f.ArticleNumber.ToLower().Contains(search) ||
                f.ViolationDescription.Uz.ToLower().Contains(search) ||
                f.ViolationDescription.UzLatin.ToLower().Contains(search) ||
                f.ViolationDescription.Ru.ToLower().Contains(search));
        }

        if (request.MinPenalty.HasValue)
            query = query.Where(f => f.PenaltyAmountTiyins >= request.MinPenalty.Value);

        if (request.MaxPenalty.HasValue)
            query = query.Where(f => f.PenaltyAmountTiyins <= request.MaxPenalty.Value);

        var projected = query
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.ArticleNumber)
            .Select(f => new FineDto(
                f.Id,
                f.ArticleNumber,
                f.ViolationDescription,
                f.AdditionalNotes,
                f.PenaltyAmountTiyins,
                f.PenaltyMaxTiyins,
                f.ImageUrl,
                f.SortOrder,
                f.IsActive));

        var result = await PaginatedList<FineDto>.CreateAsync(projected, request.Page, request.PageSize, ct);

        // resolve presigned URLs for items with images
        var itemsWithPresigned = new List<FineDto>();
        foreach (var item in result.Items)
        {
            if (item.ImageUrl is not null)
            {
                var presigned = await storage.GetPresignedUrlAsync(item.ImageUrl, ct);
                itemsWithPresigned.Add(item with { ImageUrl = presigned });
            }
            else
                itemsWithPresigned.Add(item);
        }

        var final = new PaginatedList<FineDto>(itemsWithPresigned, result.Meta.TotalCount, result.Meta.Page, result.Meta.PageSize);

        await cache.SetAsync(cacheKey, final, TimeSpan.FromHours(1), ct);
        logger.LogDebug("Fines list loaded from DB, cached for 1h");

        return ApiResponse<PaginatedList<FineDto>>.Ok(final);
    }
}
