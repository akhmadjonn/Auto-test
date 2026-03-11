using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Categories;

public record GetCategoriesTreeQuery(Language Language = Language.UzLatin) : IRequest<ApiResponse<List<CategoryTreeDto>>>;

public record CategoryTreeDto(
    Guid Id,
    string Name,
    string Slug,
    string? IconUrl,
    int SortOrder,
    int QuestionCount,
    List<CategoryTreeDto> Children);

public class GetCategoriesTreeQueryHandler(
    IApplicationDbContext db,
    ICacheService cache,
    ILogger<GetCategoriesTreeQueryHandler> logger) : IRequestHandler<GetCategoriesTreeQuery, ApiResponse<List<CategoryTreeDto>>>
{
    public async Task<ApiResponse<List<CategoryTreeDto>>> Handle(GetCategoriesTreeQuery request, CancellationToken ct)
    {
        var cacheKey = $"avtolider:categories:tree:{request.Language}";
        var cached = await cache.GetAsync<List<CategoryTreeDto>>(cacheKey, ct);
        if (cached is not null)
            return ApiResponse<List<CategoryTreeDto>>.Ok(cached);

        var categories = await db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .Include(c => c.Questions.Where(q => q.IsActive))
            .OrderBy(c => c.SortOrder)
            .ToListAsync(ct);

        var lookup = categories.ToLookup(c => c.ParentId);

        var tree = BuildTree(lookup, null, request.Language);

        await cache.SetAsync(cacheKey, tree, TimeSpan.FromHours(1), ct);
        logger.LogDebug("Categories tree loaded from DB, cached for 1h");

        return ApiResponse<List<CategoryTreeDto>>.Ok(tree);
    }

    private static List<CategoryTreeDto> BuildTree(
        ILookup<Guid?, Domain.Entities.Category> lookup,
        Guid? parentId,
        Language lang)
    {
        return lookup[parentId]
            .Select(c => new CategoryTreeDto(
                c.Id,
                c.Name.Get(lang),
                c.Slug,
                c.IconUrl,
                c.SortOrder,
                c.Questions.Count,
                BuildTree(lookup, c.Id, lang)))
            .ToList();
    }
}
