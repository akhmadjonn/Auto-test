using AutoTest.Application.Features.Glossary;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutoTest.Api.Controllers;

[ApiController]
[Route("api/v1/glossary")]
[EnableRateLimiting("anonymous")]
public class GlossaryController(IMediator mediator) : ControllerBase
{
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories(CancellationToken ct)
    {
        var result = await mediator.Send(new GetGlossaryCategoriesQuery(), ct);
        return Ok(result);
    }

    [HttpGet("categories/{slug}/terms")]
    public async Task<IActionResult> GetTermsByCategory(string slug, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetGlossaryTermsQuery(slug, page, pageSize), ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await mediator.Send(new SearchGlossaryQuery(q, page, pageSize), ct);
        return Ok(result);
    }

    [HttpGet("terms/{id}")]
    public async Task<IActionResult> GetTermById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetGlossaryTermByIdQuery(id), ct);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
