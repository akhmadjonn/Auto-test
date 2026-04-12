using AutoTest.Application.Common.Models;
using AutoTest.Application.Features.VideoLessons;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutoTest.Api.Controllers;

[ApiController]
[Route("api/v1/admin/video-lessons")]
[Authorize(Roles = "Admin")]
[EnableRateLimiting("authenticated")]
public class AdminVideoLessonsController(IMediator mediator) : ControllerBase
{
    // --- Categories ---

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories(CancellationToken ct)
    {
        var result = await mediator.Send(new GetVideoCategoriesAdminQuery(), ct);
        return Ok(result);
    }

    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory([FromBody] CreateVideoCategoryCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return result.Success ? CreatedAtAction(nameof(GetCategories), result) : BadRequest(result);
    }

    [HttpPut("categories/{id}")]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] UpdateVideoCategoryCommand command, CancellationToken ct)
    {
        if (id != command.Id)
            return BadRequest(ApiResponse.Fail("ID_MISMATCH", "Route ID does not match body ID."));

        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpDelete("categories/{id}")]
    public async Task<IActionResult> DeleteCategory(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteVideoCategoryCommand(id), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // --- Lessons ---

    [HttpGet]
    public async Task<IActionResult> GetLessons([FromQuery] Guid? categoryId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetVideoLessonsAdminQuery(categoryId), ct);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateLesson([FromBody] CreateVideoLessonCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return result.Success ? CreatedAtAction(nameof(GetLessons), result) : BadRequest(result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateLesson(Guid id, [FromBody] UpdateVideoLessonCommand command, CancellationToken ct)
    {
        if (id != command.Id)
            return BadRequest(ApiResponse.Fail("ID_MISMATCH", "Route ID does not match body ID."));

        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteLesson(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteVideoLessonCommand(id), ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    // --- File Uploads ---

    [HttpPost("{id}/upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(524_288_000)] // 500MB
    public async Task<IActionResult> UploadVideo(Guid id, IFormFile file, CancellationToken ct)
    {
        using var stream = file.OpenReadStream();
        var command = new UploadVideoFileCommand(id, stream, file.FileName, file.ContentType);
        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost("{id}/thumbnail")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadThumbnail(Guid id, IFormFile image, CancellationToken ct)
    {
        using var stream = image.OpenReadStream();
        var command = new UploadVideoThumbnailCommand(id, stream, image.FileName);
        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost("{id}/attachments")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadAttachment(Guid id, IFormFile file, CancellationToken ct)
    {
        using var stream = file.OpenReadStream();
        var command = new UploadLessonAttachmentCommand(id, stream, file.FileName, file.ContentType, file.Length);
        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{id}/attachments/{attachmentId}")]
    public async Task<IActionResult> DeleteAttachment(Guid id, Guid attachmentId, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteLessonAttachmentCommand(id, attachmentId), ct);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
