using AutoTest.Application.Features.FirstAid;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutoTest.Api.Controllers;

[ApiController]
[Route("api/v1/first-aid")]
[EnableRateLimiting("anonymous")]
public class FirstAidController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetFirstAidProceduresQuery(), ct);
        return Ok(result);
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetFirstAidProcedureQuery(slug), ct);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
