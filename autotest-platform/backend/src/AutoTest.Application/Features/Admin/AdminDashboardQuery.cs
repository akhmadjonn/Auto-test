using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.Admin;

public record AdminDashboardQuery : IRequest<ApiResponse<AdminDashboardDto>>;

public record AdminDashboardDto(
    int TotalUsers,
    int ActiveUsersToday,
    int TotalQuestions,
    int ActiveQuestions,
    int TotalExamSessions,
    int ActiveSubscriptions,
    long TotalRevenue,
    int NewUsersThisWeek,
    List<ExamModeBreakdownDto> ExamModeBreakdown,
    List<RecentUserDto> RecentUsers);

public record ExamModeBreakdownDto(ExamMode Mode, int Count);
public record RecentUserDto(Guid Id, string? PhoneNumber, string? FirstName, DateTimeOffset CreatedAt);

public class AdminDashboardQueryHandler(
    IApplicationDbContext db,
    IDateTimeProvider dateTime,
    ICacheService cache) : IRequestHandler<AdminDashboardQuery, ApiResponse<AdminDashboardDto>>
{
    public async Task<ApiResponse<AdminDashboardDto>> Handle(AdminDashboardQuery request, CancellationToken ct)
    {
        const string cacheKey = "avtolider:admin:dashboard";
        var cached = await cache.GetAsync<AdminDashboardDto>(cacheKey, ct);
        if (cached is not null)
            return ApiResponse<AdminDashboardDto>.Ok(cached);

        var now = dateTime.UtcNow;
        var today = new DateTimeOffset(now.Date, TimeSpan.Zero);
        var weekAgo = now.AddDays(-7);

        // Sequential queries — EF Core DbContext is not thread-safe
        var totalUsers = await db.Users.CountAsync(ct);
        var activeUsersToday = await db.Users.CountAsync(u => u.LastActiveAt >= today, ct);
        var totalQuestions = await db.Questions.CountAsync(ct);
        var activeQuestions = await db.Questions.CountAsync(q => q.IsActive, ct);
        var totalSessions = await db.ExamSessions.CountAsync(ct);
        var activeSubs = await db.Subscriptions.CountAsync(s => s.Status == SubscriptionStatus.Active && s.ExpiresAt > now, ct);
        var totalRevenue = await db.PaymentTransactions
            .Where(p => p.Status == PaymentStatus.Completed)
            .Select(p => (long?)p.AmountInTiyins)
            .SumAsync(ct) ?? 0L;
        var newUsersWeek = await db.Users.CountAsync(u => u.CreatedAt >= weekAgo, ct);

        var examModes = await db.ExamSessions
            .AsNoTracking()
            .GroupBy(s => s.Mode)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var recentUsers = await db.Users
            .AsNoTracking()
            .OrderByDescending(u => u.CreatedAt)
            .Take(5)
            .Select(u => new RecentUserDto(u.Id, u.PhoneNumber, u.FirstName, u.CreatedAt))
            .ToListAsync(ct);

        var dto = new AdminDashboardDto(
            totalUsers, activeUsersToday, totalQuestions, activeQuestions,
            totalSessions, activeSubs, totalRevenue, newUsersWeek,
            examModes.Select(e => new ExamModeBreakdownDto(e.Key, e.Count)).ToList(),
            recentUsers);

        // Short TTL — admin dashboard shows near-real-time stats
        await cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(2), ct);
        return ApiResponse<AdminDashboardDto>.Ok(dto);
    }
}
