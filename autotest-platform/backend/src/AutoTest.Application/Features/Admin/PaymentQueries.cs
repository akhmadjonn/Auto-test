using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.Admin;

// Payment transaction listing
public record GetPaymentTransactionsQuery(
    Guid? UserId = null,
    PaymentProvider? Provider = null,
    PaymentStatus? Status = null,
    DateTimeOffset? DateFrom = null,
    DateTimeOffset? DateTo = null,
    int Page = 1,
    int PageSize = 20) : IRequest<ApiResponse<PaginatedList<PaymentTransactionDto>>>;

public record PaymentTransactionDto(
    Guid Id, Guid UserId, string? UserPhone,
    PaymentProvider Provider, string? ProviderTransactionId,
    long AmountInTiyins, string Currency, PaymentStatus Status,
    DateTimeOffset? CompletedAt, DateTimeOffset CreatedAt);

public class GetPaymentTransactionsQueryHandler(
    IApplicationDbContext db) : IRequestHandler<GetPaymentTransactionsQuery, ApiResponse<PaginatedList<PaymentTransactionDto>>>
{
    public async Task<ApiResponse<PaginatedList<PaymentTransactionDto>>> Handle(GetPaymentTransactionsQuery request, CancellationToken ct)
    {
        var query = db.PaymentTransactions
            .AsNoTracking()
            .Include(p => p.User)
            .AsQueryable();

        if (request.UserId.HasValue)
            query = query.Where(p => p.UserId == request.UserId.Value);

        if (request.Provider.HasValue)
            query = query.Where(p => p.Provider == request.Provider.Value);

        if (request.Status.HasValue)
            query = query.Where(p => p.Status == request.Status.Value);

        if (request.DateFrom.HasValue)
            query = query.Where(p => p.CreatedAt >= request.DateFrom.Value);

        if (request.DateTo.HasValue)
            query = query.Where(p => p.CreatedAt <= request.DateTo.Value);

        var projected = query
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PaymentTransactionDto(
                p.Id, p.UserId, p.User.PhoneNumber,
                p.Provider, p.ProviderTransactionId,
                p.AmountInTiyins, p.Currency, p.Status,
                p.CompletedAt, p.CreatedAt));

        var result = await PaginatedList<PaymentTransactionDto>.CreateAsync(projected, request.Page, request.PageSize, ct);
        return ApiResponse<PaginatedList<PaymentTransactionDto>>.Ok(result);
    }
}

// Revenue report
public record GetRevenueReportQuery(
    DateTimeOffset? DateFrom = null,
    DateTimeOffset? DateTo = null) : IRequest<ApiResponse<RevenueReportDto>>;

public record RevenueReportDto(
    long TotalRevenue,
    int TotalTransactions,
    int CompletedTransactions,
    int FailedTransactions,
    long PaymeRevenue,
    long ClickRevenue,
    List<DailyRevenueDto> DailyBreakdown);

public record DailyRevenueDto(DateOnly Date, long Revenue, int TransactionCount);

public class GetRevenueReportQueryHandler(
    IApplicationDbContext db) : IRequestHandler<GetRevenueReportQuery, ApiResponse<RevenueReportDto>>
{
    public async Task<ApiResponse<RevenueReportDto>> Handle(GetRevenueReportQuery request, CancellationToken ct)
    {
        var query = db.PaymentTransactions.AsNoTracking().AsQueryable();

        if (request.DateFrom.HasValue)
            query = query.Where(p => p.CreatedAt >= request.DateFrom.Value);

        if (request.DateTo.HasValue)
            query = query.Where(p => p.CreatedAt <= request.DateTo.Value);

        // DB-level aggregation — no ToListAsync, no in-memory processing
        var totalTransactions = await query.CountAsync(ct);
        var completedQuery = query.Where(p => p.Status == PaymentStatus.Completed);
        var completedCount = await completedQuery.CountAsync(ct);
        var failedCount = await query.CountAsync(p => p.Status == PaymentStatus.Failed, ct);
        var totalRevenue = await completedQuery.SumAsync(p => (long?)p.AmountInTiyins, ct) ?? 0;
        var paymeRevenue = await completedQuery
            .Where(p => p.Provider == PaymentProvider.Payme)
            .SumAsync(p => (long?)p.AmountInTiyins, ct) ?? 0;
        var clickRevenue = await completedQuery
            .Where(p => p.Provider == PaymentProvider.Click)
            .SumAsync(p => (long?)p.AmountInTiyins, ct) ?? 0;

        // Daily breakdown via DB GroupBy — use Year/Month/Day for Npgsql compatibility
        var dailyRaw = await completedQuery
            .GroupBy(p => new { p.CompletedAt!.Value.Year, p.CompletedAt!.Value.Month, p.CompletedAt!.Value.Day })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Revenue = g.Sum(p => p.AmountInTiyins), Count = g.Count() })
            .OrderByDescending(d => d.Year).ThenByDescending(d => d.Month).ThenByDescending(d => d.Day)
            .ToListAsync(ct);

        var daily = dailyRaw
            .Select(d => new DailyRevenueDto(new DateOnly(d.Year, d.Month, d.Day), d.Revenue, d.Count))
            .ToList();

        return ApiResponse<RevenueReportDto>.Ok(new RevenueReportDto(
            totalRevenue, totalTransactions, completedCount, failedCount,
            paymeRevenue, clickRevenue, daily));
    }
}

// Export revenue report (Excel)
public record ExportRevenueReportCommand(
    DateTimeOffset? DateFrom = null,
    DateTimeOffset? DateTo = null) : IRequest<ApiResponse<Stream>>;

public class ExportRevenueReportCommandHandler(
    IExcelExportService excelService) : IRequestHandler<ExportRevenueReportCommand, ApiResponse<Stream>>
{
    public async Task<ApiResponse<Stream>> Handle(ExportRevenueReportCommand request, CancellationToken ct)
    {
        var stream = await excelService.ExportRevenueReportAsync(request.DateFrom, request.DateTo, ct);
        return ApiResponse<Stream>.Ok(stream);
    }
}
