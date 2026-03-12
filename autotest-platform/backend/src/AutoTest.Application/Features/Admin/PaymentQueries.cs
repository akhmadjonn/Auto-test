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

        var transactions = await query.ToListAsync(ct);

        var completed = transactions.Where(t => t.Status == PaymentStatus.Completed).ToList();
        var totalRevenue = completed.Sum(t => t.AmountInTiyins);
        var paymeRevenue = completed.Where(t => t.Provider == PaymentProvider.Payme).Sum(t => t.AmountInTiyins);
        var clickRevenue = completed.Where(t => t.Provider == PaymentProvider.Click).Sum(t => t.AmountInTiyins);

        var daily = completed
            .GroupBy(t => DateOnly.FromDateTime(t.CompletedAt?.DateTime ?? t.CreatedAt.DateTime))
            .Select(g => new DailyRevenueDto(g.Key, g.Sum(t => t.AmountInTiyins), g.Count()))
            .OrderByDescending(d => d.Date)
            .ToList();

        return ApiResponse<RevenueReportDto>.Ok(new RevenueReportDto(
            totalRevenue,
            transactions.Count,
            completed.Count,
            transactions.Count(t => t.Status == PaymentStatus.Failed),
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
