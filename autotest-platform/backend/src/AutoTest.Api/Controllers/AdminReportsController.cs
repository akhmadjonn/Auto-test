using AutoTest.Application.Features.Admin;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutoTest.Api.Controllers;

[ApiController]
[Route("api/v1/admin/reports")]
[Authorize(Roles = "Admin")]
[EnableRateLimiting("authenticated")]
public class AdminReportsController(IMediator mediator) : ControllerBase
{
    [HttpGet("users/export")]
    public async Task<IActionResult> ExportUsers(
        [FromQuery] DateTimeOffset? dateFrom,
        [FromQuery] DateTimeOffset? dateTo,
        [FromQuery] string? subscriptionStatus,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new ExportUsersReportCommand(dateFrom, dateTo, subscriptionStatus), ct);
        return File(result.Data!, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "users-report.xlsx");
    }

    [HttpGet("exams/export")]
    public async Task<IActionResult> ExportExamStats(
        [FromQuery] DateTimeOffset? dateFrom,
        [FromQuery] DateTimeOffset? dateTo,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new ExportExamStatsReportCommand(dateFrom, dateTo), ct);
        return File(result.Data!, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "exam-stats-report.xlsx");
    }

    [HttpGet("questions/export")]
    public async Task<IActionResult> ExportQuestions(CancellationToken ct)
    {
        var result = await mediator.Send(new ExportQuestionsReportCommand(), ct);
        return File(result.Data!, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "questions-report.xlsx");
    }
}
