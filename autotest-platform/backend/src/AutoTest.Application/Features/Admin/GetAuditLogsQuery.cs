using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.Admin;

public record GetAuditLogsQuery(
    Guid? UserId = null,
    AuditAction? Action = null,
    string? EntityType = null,
    DateTimeOffset? DateFrom = null,
    DateTimeOffset? DateTo = null,
    int Page = 1,
    int PageSize = 20) : IRequest<ApiResponse<PaginatedList<AuditLogDto>>>;

public record AuditLogDto(
    Guid Id,
    Guid UserId,
    string? UserName,
    AuditAction Action,
    string EntityType,
    string? EntityId,
    string? OldValues,
    string? NewValues,
    string? IpAddress,
    DateTimeOffset CreatedAt);

public class GetAuditLogsQueryHandler(
    IApplicationDbContext db) : IRequestHandler<GetAuditLogsQuery, ApiResponse<PaginatedList<AuditLogDto>>>
{
    public async Task<ApiResponse<PaginatedList<AuditLogDto>>> Handle(GetAuditLogsQuery request, CancellationToken ct)
    {
        var query = db.AuditLogs.AsNoTracking().AsQueryable();

        if (request.UserId.HasValue)
            query = query.Where(a => a.UserId == request.UserId.Value);

        if (request.Action.HasValue)
            query = query.Where(a => a.Action == request.Action.Value);

        if (request.EntityType is not null)
            query = query.Where(a => a.EntityType == request.EntityType);

        if (request.DateFrom.HasValue)
            query = query.Where(a => a.CreatedAt >= request.DateFrom.Value);

        if (request.DateTo.HasValue)
            query = query.Where(a => a.CreatedAt <= request.DateTo.Value);

        var ordered = query.OrderByDescending(a => a.CreatedAt);

        var userIds = await ordered
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => a.UserId)
            .Distinct()
            .ToListAsync(ct);

        var userNames = await db.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim(), ct);

        var projected = ordered.Select(a => new AuditLogDto(
            a.Id,
            a.UserId,
            null!, // filled below
            a.Action,
            a.EntityType,
            a.EntityId,
            a.OldValues,
            a.NewValues,
            a.IpAddress,
            a.CreatedAt));

        var paginated = await PaginatedList<AuditLogDto>.CreateAsync(projected, request.Page, request.PageSize, ct);

        // Enrich with user names
        var enriched = paginated.Items.Select(a => a with
        {
            UserName = userNames.TryGetValue(a.UserId, out var name) ? name : null
        }).ToList();

        return ApiResponse<PaginatedList<AuditLogDto>>.Ok(
            new PaginatedList<AuditLogDto>(enriched, paginated.Meta.TotalCount, request.Page, request.PageSize));
    }
}
