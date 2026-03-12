using AutoTest.Application.Features.Subscriptions;
using AutoTest.Domain.Common.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutoTest.Api.Controllers;

[ApiController]
[Route("api/subscriptions")]
[Authorize]
[EnableRateLimiting("authenticated")]
public class SubscriptionsController(ISender mediator) : ControllerBase
{
    [HttpGet("plans")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPlans(
        [FromQuery] Language language = Language.UzLatin,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetPlansQuery(language), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(
        [FromQuery] Language language = Language.UzLatin,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetSubscriptionStatusQuery(language), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateSubscriptionCommand command,
        CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{subscriptionId:guid}")]
    public async Task<IActionResult> Cancel(Guid subscriptionId, CancellationToken ct)
    {
        var result = await mediator.Send(new CancelSubscriptionCommand(subscriptionId), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
