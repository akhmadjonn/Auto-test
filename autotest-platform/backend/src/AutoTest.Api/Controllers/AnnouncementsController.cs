using AutoTest.Application.Features.Admin;
using AutoTest.Domain.Common.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutoTest.Api.Controllers;

[ApiController]
[Route("api/v1/announcements")]
[EnableRateLimiting("anonymous")]
public class AnnouncementsController(IMediator mediator) : ControllerBase
{
    [HttpGet("active")]
    public async Task<IActionResult> GetActive([FromQuery] Language language = Language.UzLatin, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetActiveAnnouncementsQuery(language), ct);
        return Ok(result);
    }
}
