using AutoTest.Application.Features.RoadMarkings;
using AutoTest.Domain.Common.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutoTest.Api.Controllers;

[ApiController]
[Route("api/v1/road-markings")]
[EnableRateLimiting("anonymous")]
public class RoadMarkingsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] RoadMarkingType? type, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetRoadMarkingsQuery(type), ct);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetRoadMarkingByIdQuery(id), ct);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
