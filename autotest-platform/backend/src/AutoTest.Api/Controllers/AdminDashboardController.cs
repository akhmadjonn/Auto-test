using AutoTest.Application.Features.Admin;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutoTest.Api.Controllers;

[ApiController]
[Route("api/v1/admin/dashboard")]
[Authorize(Roles = "Admin")]
[EnableRateLimiting("authenticated")]
public class AdminDashboardController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var result = await mediator.Send(new AdminDashboardQuery(), ct);
        return Ok(result);
    }
}
