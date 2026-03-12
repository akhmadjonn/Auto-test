using AutoTest.Application.Features.Categories;
using AutoTest.Domain.Common.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutoTest.Api.Controllers;

[ApiController]
[Route("api/v1/categories")]
[EnableRateLimiting("anonymous")]
public class CategoriesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetTree([FromQuery] Language language = Language.UzLatin, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetCategoriesTreeQuery(language), ct);
        return Ok(result);
    }
}
