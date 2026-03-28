using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Glossary;

public record GetGlossaryCategoriesQuery : IRequest<ApiResponse<List<GlossaryCategoryDto>>>;

public record GlossaryCategoryDto(
    Guid Id,
    string Slug,
    LocalizedText Name,
    string? Icon,
    int SortOrder,
    int TermCount);

public class GetGlossaryCategoriesQueryHandler(
    IApplicationDbContext db,
    ICacheService cache,
    ILogger<GetGlossaryCategoriesQueryHandler> logger) : IRequestHandler<GetGlossaryCategoriesQuery, ApiResponse<List<GlossaryCategoryDto>>>
{
    private const string CacheKey = "avtolider:glossary:categories";

    public async Task<ApiResponse<List<GlossaryCategoryDto>>> Handle(GetGlossaryCategoriesQuery request, CancellationToken ct)
    {
        var cached = await cache.GetAsync<List<GlossaryCategoryDto>>(CacheKey, ct);
        if (cached is not null)
            return ApiResponse<List<GlossaryCategoryDto>>.Ok(cached);

        var entities = await db.GlossaryCategories
            .AsNoTracking()
            .Include(c => c.Terms)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(ct);

        var categories = entities.Select(c => new GlossaryCategoryDto(
            c.Id,
            c.Slug,
            c.Name,
            c.Icon,
            c.SortOrder,
            c.Terms.Count)).ToList();

        await cache.SetAsync(CacheKey, categories, TimeSpan.FromHours(6), ct);
        logger.LogDebug("Glossary categories loaded from DB, cached for 6h");

        return ApiResponse<List<GlossaryCategoryDto>>.Ok(categories);
    }

    internal static async Task InvalidateGlossaryCategoryCacheAsync(ICacheService cache, CancellationToken ct) =>
        await cache.RemoveAsync(CacheKey, ct);
}
