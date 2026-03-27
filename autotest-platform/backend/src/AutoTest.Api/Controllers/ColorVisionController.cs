using AutoTest.Application.Features.ColorVision;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutoTest.Api.Controllers;

[ApiController]
[Route("api/v1/color-vision")]
[EnableRateLimiting("anonymous")]
public class ColorVisionController(IMediator mediator) : ControllerBase
{
    [HttpGet("plates")]
    public async Task<IActionResult> GetPlates(CancellationToken ct)
    {
        var result = await mediator.Send(new GetColorVisionPlatesQuery(), ct);
        return Ok(result);
    }

    [HttpPost("result")]
    [Authorize]
    public async Task<IActionResult> SaveResult([FromBody] SaveColorVisionResultCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
