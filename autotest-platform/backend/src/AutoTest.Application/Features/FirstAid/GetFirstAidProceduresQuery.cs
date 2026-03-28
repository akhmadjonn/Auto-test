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

        var procedures = entities.Select(p => new FirstAidProcedureListDto(
            p.Id,
            p.Slug,
            p.Name,
            p.Summary,
            p.IconUrl,
            p.SortOrder,
            p.Steps.Count)).ToList();

        await cache.SetAsync(CacheKey, procedures, TimeSpan.FromHours(24), ct);
        logger.LogDebug("First aid procedures loaded from DB, cached for 24h");

        return ApiResponse<List<FirstAidProcedureListDto>>.Ok(procedures);
    }
}
