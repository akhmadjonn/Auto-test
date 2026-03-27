using AutoTest.Application.Features.TrafficFines;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutoTest.Api.Controllers;

[ApiController]
[Route("api/v1/fines")]
[EnableRateLimiting("anonymous")]
public class FinesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] long? minPenalty = null,
        [FromQuery] long? maxPenalty = null,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetFinesQuery(page, pageSize, search, minPenalty, maxPenalty), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetFineByIdQuery(id), ct);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
