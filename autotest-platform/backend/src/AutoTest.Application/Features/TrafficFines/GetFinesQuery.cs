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

        var ordered = query
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.ArticleNumber);

        var totalCount = await ordered.CountAsync(ct);
        var entities = await ordered
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var items = new List<FineDto>();
        foreach (var f in entities)
        {
            string? presignedUrl = f.ImageUrl is not null
                ? await storage.GetPresignedUrlAsync(f.ImageUrl, ct)
                : null;

            items.Add(new FineDto(
                f.Id,
                f.ArticleNumber,
                f.ViolationDescription,
                f.AdditionalNotes,
                f.PenaltyAmountTiyins,
                f.PenaltyMaxTiyins,
                presignedUrl,
                f.SortOrder,
                f.IsActive));
        }

        var final = new PaginatedList<FineDto>(items, totalCount, request.Page, request.PageSize);

        await cache.SetAsync(cacheKey, final, TimeSpan.FromMinutes(50), ct);
        logger.LogDebug("Fines list loaded from DB, cached for 50min");

        return ApiResponse<PaginatedList<FineDto>>.Ok(final);
    }
}
