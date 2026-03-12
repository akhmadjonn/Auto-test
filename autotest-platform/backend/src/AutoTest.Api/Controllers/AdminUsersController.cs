using AutoTest.Application.Features.Admin;
using AutoTest.Domain.Common.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutoTest.Api.Controllers;

[ApiController]
[Route("api/v1/admin/users")]
[Authorize(Roles = "Admin")]
[EnableRateLimiting("authenticated")]
public class AdminUsersController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] UserRole? role,
        [FromQuery] bool? isBlocked,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetUsersListQuery(search, role, isBlocked, page, pageSize), ct);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetUserDetailQuery(id), ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPatch("{id}/role")]
    public async Task<IActionResult> UpdateRole(Guid id, [FromBody] UpdateRoleRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateUserRoleCommand(id, req.Role), ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPatch("{id}/block")]
    public async Task<IActionResult> ToggleBlock(Guid id, [FromBody] ToggleBlockRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new ToggleUserBlockCommand(id, req.IsBlocked), ct);
        return result.Success ? Ok(result) : NotFound(result);
    }
}

public record UpdateRoleRequest(UserRole Role);
public record ToggleBlockRequest(bool IsBlocked);
