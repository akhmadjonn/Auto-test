using AutoTest.Application.Features.Admin;
using AutoTest.Domain.Common.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutoTest.Api.Controllers;

[ApiController]
[Route("api/v1/admin/payments")]
[Authorize(Roles = "Admin")]
[EnableRateLimiting("authenticated")]
public class AdminPaymentsController(IMediator mediator) : ControllerBase
{
    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] Guid? userId,
        [FromQuery] PaymentProvider? provider,
        [FromQuery] PaymentStatus? status,
        [FromQuery] DateTimeOffset? dateFrom,
        [FromQuery] DateTimeOffset? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new GetPaymentTransactionsQuery(userId, provider, status, dateFrom, dateTo, page, pageSize), ct);
        return Ok(result);
    }

    [HttpGet("revenue")]
    public async Task<IActionResult> GetRevenueReport(
        [FromQuery] DateTimeOffset? dateFrom,
        [FromQuery] DateTimeOffset? dateTo,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetRevenueReportQuery(dateFrom, dateTo), ct);
        return Ok(result);
    }

    [HttpGet("revenue/export")]
    public async Task<IActionResult> ExportRevenue(
        [FromQuery] DateTimeOffset? dateFrom,
        [FromQuery] DateTimeOffset? dateTo,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new ExportRevenueReportCommand(dateFrom, dateTo), ct);
        return File(result.Data!, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "revenue-report.xlsx");
    }
}
