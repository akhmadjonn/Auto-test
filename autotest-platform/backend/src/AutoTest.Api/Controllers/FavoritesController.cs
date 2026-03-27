using AutoTest.Application.Features.Favorites;
using AutoTest.Domain.Common.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutoTest.Api.Controllers;

[ApiController]
[Route("api/v1/questions")]
[Authorize]
[EnableRateLimiting("authenticated")]
public class FavoritesController(ISender mediator) : ControllerBase
{
    [HttpPost("{questionId:guid}/favorite")]
    public async Task<IActionResult> ToggleFavorite(Guid questionId, CancellationToken ct)
    {
        var result = await mediator.Send(new ToggleFavoriteCommand(questionId), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("favorites")]
    public async Task<IActionResult> GetFavorites(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Language language = Language.UzLatin,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetFavoritesQuery(page, pageSize, language), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
