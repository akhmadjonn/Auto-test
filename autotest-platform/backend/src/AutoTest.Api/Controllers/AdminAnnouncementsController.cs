using AutoTest.Application.Features.Admin;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutoTest.Api.Controllers;

[ApiController]
[Route("api/v1/admin/announcements")]
[Authorize(Roles = "Admin")]
[EnableRateLimiting("authenticated")]
public class AdminAnnouncementsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetAnnouncementsQuery(page, pageSize), ct);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAnnouncementCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return result.Success ? CreatedAtAction(nameof(GetAll), result) : BadRequest(result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAnnouncementBody body, CancellationToken ct)
    {
        var command = new UpdateAnnouncementCommand(
            id, body.Title, body.Content, body.Type, body.IsActive,
            body.StartsAt, body.ExpiresAt);
        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteAnnouncementCommand(id), ct);
        return result.Success ? Ok(result) : NotFound(result);
    }
}

public record UpdateAnnouncementBody(
    LocalizedText Title,
    LocalizedText Content,
    AnnouncementType Type,
    bool IsActive,
    DateTimeOffset? StartsAt,
    DateTimeOffset? ExpiresAt);
