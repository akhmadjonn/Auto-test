using AutoTest.Application.Features.VideoLessons;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutoTest.Api.Controllers;

[ApiController]
[Route("api/v1/video-lessons")]
[Authorize]
[EnableRateLimiting("authenticated")]
public class VideoLessonsController(IMediator mediator) : ControllerBase
{
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories(CancellationToken ct)
    {
        var result = await mediator.Send(new GetVideoCategoriesQuery(), ct);
        return Ok(result);
    }

    [HttpGet("categories/{categoryId}/lessons")]
    public async Task<IActionResult> GetLessonsByCategory(Guid categoryId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetVideoLessonsByCategoryQuery(categoryId), ct);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetLesson(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetVideoLessonByIdQuery(id), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id}/progress")]
    public async Task<IActionResult> UpdateProgress(Guid id, [FromBody] UpdateProgressRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateLessonProgressCommand(id, request.WatchedSeconds), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

public record UpdateProgressRequest(int WatchedSeconds);
