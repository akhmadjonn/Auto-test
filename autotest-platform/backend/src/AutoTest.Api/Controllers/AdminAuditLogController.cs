using AutoTest.Application.Features.Admin;
using AutoTest.Domain.Common.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoTest.Api.Controllers;

[ApiController]
[Route("api/v1/admin/audit-logs")]
[Authorize(Roles = "Admin")]
public class AdminAuditLogController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetLogs(
        [FromQuery] Guid? userId,
        [FromQuery] AuditAction? action,
        [FromQuery] string? entityType,
        [FromQuery] DateTimeOffset? dateFrom,
        [FromQuery] DateTimeOffset? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetAuditLogsQuery(userId, action, entityType, dateFrom, dateTo, page, pageSize), ct);
        return Ok(result);
    }
}
