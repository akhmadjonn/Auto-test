using AutoTest.Application.Features.Leaderboard;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutoTest.Api.Controllers;

[ApiController]
[Route("api/v1/leaderboard")]
[Authorize]
[EnableRateLimiting("authenticated")]
public class LeaderboardController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetLeaderboard(
        [FromQuery] string period = "weekly",
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetLeaderboardQuery(period, limit), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
