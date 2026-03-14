using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Categories;

public record GetCategoriesTreeQuery(Language Language = Language.UzLatin) : IRequest<ApiResponse<List<CategoryTreeDto>>>;

public record CategoryTreeDto(
    Guid Id,
    LocalizedText Name,
    LocalizedText Description,
    string? IconUrl,
    int QuestionCount,
    List<CategoryTreeDto> Children);

public class GetCategoriesTreeQueryHandler(
    IApplicationDbContext db,
    ICacheService cache,
    ILogger<GetCategoriesTreeQueryHandler> logger) : IRequestHandler<GetCategoriesTreeQuery, ApiResponse<List<CategoryTreeDto>>>
{
    public async Task<ApiResponse<List<CategoryTreeDto>>> Handle(GetCategoriesTreeQuery request, CancellationToken ct)
    {
        var cacheKey = "avtolider:categories:tree:all";
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

        var tree = BuildTree(lookup, null);

        await cache.SetAsync(cacheKey, tree, TimeSpan.FromHours(1), ct);
        logger.LogDebug("Categories tree loaded from DB, cached for 1h");

        return ApiResponse<List<CategoryTreeDto>>.Ok(tree);
    }

    private static List<CategoryTreeDto> BuildTree(
        ILookup<Guid?, Domain.Entities.Category> lookup,
        Guid? parentId)
    {
        return lookup[parentId]
            .Select(c => new CategoryTreeDto(
                c.Id,
                c.Name,
                c.Description,
                c.IconUrl,
                c.Questions.Count,
                BuildTree(lookup, c.Id)))
            .ToList();
    }
}
