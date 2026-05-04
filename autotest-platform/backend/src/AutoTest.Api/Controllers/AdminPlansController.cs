using AutoTest.Application.Features.Admin;
using AutoTest.Domain.Common.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutoTest.Api.Controllers;

[ApiController]
[Route("api/v1/admin/plans")]
[Authorize(Roles = "Admin")]
[EnableRateLimiting("authenticated")]
public class AdminPlansController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await mediator.Send(new GetAdminPlansQuery(), ct);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePlanCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return result.Success ? CreatedAtAction(nameof(GetAll), result) : BadRequest(result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePlanBody body, CancellationToken ct)
    {
        var command = new UpdatePlanCommand(
            id, body.Name, body.Description, body.PriceInTiyins,
            body.DurationDays, body.Features);
        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> ToggleStatus(Guid id, [FromBody] TogglePlanStatusRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new TogglePlanStatusCommand(id, req.IsActive), ct);
        return result.Success ? Ok(result) : NotFound(result);
    }
}

public record TogglePlanStatusRequest(bool IsActive);

public record UpdatePlanBody(
    LocalizedText Name,
    LocalizedText Description,
    long PriceInTiyins,
    int DurationDays,
    string Features);
