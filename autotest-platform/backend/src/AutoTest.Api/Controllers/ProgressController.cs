using AutoTest.Application.Features.Progress;
using AutoTest.Domain.Common.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutoTest.Api.Controllers;

[ApiController]
[Route("api/v1/progress")]
[Authorize]
[EnableRateLimiting("authenticated")]
public class ProgressController(ISender mediator) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] Language language = Language.UzLatin,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetUserDashboardQuery(language), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategoryPerformance(
        [FromQuery] Language language = Language.UzLatin,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetCategoryPerformanceQuery(language), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
