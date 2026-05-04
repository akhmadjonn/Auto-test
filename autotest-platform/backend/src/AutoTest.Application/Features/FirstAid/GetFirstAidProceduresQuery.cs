using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.FirstAid;

public record GetFirstAidProceduresQuery : IRequest<ApiResponse<List<FirstAidProcedureListDto>>>;

public record FirstAidProcedureListDto(
    Guid Id,
    string Slug,
    LocalizedText Name,
    LocalizedText? Summary,
    string IconUrl,
    int SortOrder,
    int StepCount);

public class GetFirstAidProceduresQueryHandler(
    IApplicationDbContext db,
    IFileStorageService storage,
    ICacheService cache,
    ILogger<GetFirstAidProceduresQueryHandler> logger) : IRequestHandler<GetFirstAidProceduresQuery, ApiResponse<List<FirstAidProcedureListDto>>>
{
    private const string CacheKey = "avtolider:first-aid:all";

    public async Task<ApiResponse<List<FirstAidProcedureListDto>>> Handle(GetFirstAidProceduresQuery request, CancellationToken ct)
    {
        var cached = await cache.GetAsync<List<FirstAidProcedureListDto>>(CacheKey, ct);
        if (cached is not null)
            return ApiResponse<List<FirstAidProcedureListDto>>.Ok(cached);

        var entities = await db.FirstAidProcedures
            .AsNoTracking()
            .Include(p => p.Steps)
            .OrderBy(p => p.SortOrder)
            .ToListAsync(ct);

        var procedures = new List<FirstAidProcedureListDto>(entities.Count);
        foreach (var p in entities)
        {
            var iconUrl = string.IsNullOrEmpty(p.IconUrl)
                ? string.Empty
                : await storage.GetPresignedUrlAsync(p.IconUrl, ct);

            procedures.Add(new FirstAidProcedureListDto(
                p.Id,
                p.Slug,
                p.Name,
                p.Summary,
                iconUrl,
                p.SortOrder,
                p.Steps.Count));
        }

        // Cache TTL must stay under the presigned URL expiry (1h) to avoid stale URLs.
        await cache.SetAsync(CacheKey, procedures, TimeSpan.FromMinutes(50), ct);
        logger.LogDebug("First aid procedures loaded from DB, cached for 50min");

        return ApiResponse<List<FirstAidProcedureListDto>>.Ok(procedures);
    }
}
