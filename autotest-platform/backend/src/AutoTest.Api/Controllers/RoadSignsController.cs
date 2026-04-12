using AutoTest.Application.Features.RoadSigns;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutoTest.Api.Controllers;

[ApiController]
[Route("api/v1/road-signs")]
[EnableRateLimiting("anonymous")]
public class RoadSignsController(IMediator mediator) : ControllerBase
{
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories(CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetRoadSignCategoriesQuery(), ct);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? categoryId, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetRoadSignsByCategoryQuery(categoryId), ct);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetRoadSignByIdQuery(id), ct);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
