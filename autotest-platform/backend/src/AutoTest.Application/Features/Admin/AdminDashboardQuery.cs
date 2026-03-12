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
    IDateTimeProvider dateTime) : IRequestHandler<AdminDashboardQuery, ApiResponse<AdminDashboardDto>>
{
    public async Task<ApiResponse<AdminDashboardDto>> Handle(AdminDashboardQuery request, CancellationToken ct)
    {
        var now = dateTime.UtcNow;
        var today = now.Date;
        var weekAgo = now.AddDays(-7);

        // Parallel queries for dashboard stats
        var totalUsersTask = db.Users.CountAsync(ct);
        var activeUsersTodayTask = db.Users.CountAsync(u => u.LastActiveAt >= today, ct);
        var totalQuestionsTask = db.Questions.CountAsync(ct);
        var activeQuestionsTask = db.Questions.CountAsync(q => q.IsActive, ct);
        var totalSessionsTask = db.ExamSessions.CountAsync(ct);
        var activeSubsTask = db.Subscriptions.CountAsync(s => s.Status == SubscriptionStatus.Active && s.ExpiresAt > now, ct);
        var totalRevenueTask = db.PaymentTransactions
            .Where(p => p.Status == PaymentStatus.Completed)
            .SumAsync(p => p.AmountInTiyins, ct);
        var newUsersWeekTask = db.Users.CountAsync(u => u.CreatedAt >= weekAgo, ct);

        await Task.WhenAll(totalUsersTask, activeUsersTodayTask, totalQuestionsTask, activeQuestionsTask,
            totalSessionsTask, activeSubsTask, totalRevenueTask, newUsersWeekTask);

        var examModes = await db.ExamSessions
            .AsNoTracking()
            .GroupBy(s => s.Mode)
            .Select(g => new ExamModeBreakdownDto(g.Key, g.Count()))
            .ToListAsync(ct);

        var recentUsers = await db.Users
            .AsNoTracking()
            .OrderByDescending(u => u.CreatedAt)
            .Take(5)
            .Select(u => new RecentUserDto(u.Id, u.PhoneNumber, u.FirstName, u.CreatedAt))
            .ToListAsync(ct);

        return ApiResponse<AdminDashboardDto>.Ok(new AdminDashboardDto(
            await totalUsersTask,
            await activeUsersTodayTask,
            await totalQuestionsTask,
            await activeQuestionsTask,
            await totalSessionsTask,
            await activeSubsTask,
            await totalRevenueTask,
            await newUsersWeekTask,
            examModes,
            recentUsers));
    }
}
