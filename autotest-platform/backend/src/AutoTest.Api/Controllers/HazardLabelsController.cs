using AutoTest.Application.Features.HazardLabels;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutoTest.Api.Controllers;

[ApiController]
[Route("api/v1/hazard-labels")]
[EnableRateLimiting("anonymous")]
public class HazardLabelsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetHazardLabelsQuery(), ct);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetHazardLabelByIdQuery(id), ct);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
