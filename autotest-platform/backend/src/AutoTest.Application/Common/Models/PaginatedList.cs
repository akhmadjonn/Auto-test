using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Common.Models;

public class PaginatedList<T>
{
    public List<T> Items { get; }
    public PaginationMeta Meta { get; }

    public PaginatedList(List<T> items, int totalCount, int page, int pageSize)
    {
        Items = items;
        Meta = new PaginationMeta(page, pageSize, totalCount, (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public static async Task<PaginatedList<T>> CreateAsync(IQueryable<T> source, int page, int pageSize, CancellationToken ct = default)
    {
        var totalCount = await source.CountAsync(ct);
        var items = await source.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PaginatedList<T>(items, totalCount, page, pageSize);
    }
}

public record PaginationMeta(int Page, int PageSize, int TotalCount, int TotalPages);
