using AutoTest.Application.Features.AnimatedTest;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutoTest.Api.Controllers;

[ApiController]
[Route("api/v1/animated-sessions")]
[Authorize]
[EnableRateLimiting("authenticated")]
public class AnimatedSessionsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] SubmitAnimatedSessionRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new SubmitAnimatedSessionCommand(request), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetSessions([FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetAnimatedSessionsQuery(page, pageSize), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetAnimatedSessionDetailQuery(id), ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var result = await mediator.Send(new GetAnimatedSessionStatsQuery(), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
