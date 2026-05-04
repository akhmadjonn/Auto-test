using AutoTest.Application.Common.Models;
using AutoTest.Application.Features.VideoLessons;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;
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
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] UpdateVideoCategoryBody body, CancellationToken ct)
    {
        // Id from route — frontend doesn't put it in the body.
        var command = new UpdateVideoCategoryCommand(id, body.Name, body.Description, body.SortOrder, body.IsActive);
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
    public async Task<IActionResult> UpdateLesson(Guid id, [FromBody] UpdateVideoLessonBody body, CancellationToken ct)
    {
        var command = new UpdateVideoLessonCommand(
            id, body.Title, body.Description, body.SourceType,
            body.VideoUrl, body.ThumbnailUrl, body.DurationSeconds,
            body.SortOrder, body.IsFree, body.IsDownloadable,
            body.LinkedCategoryId, body.IsActive);
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

public record UpdateVideoCategoryBody(
    LocalizedText Name,
    LocalizedText? Description,
    int SortOrder,
    bool IsActive);

public record UpdateVideoLessonBody(
    LocalizedText Title,
    LocalizedText? Description,
    VideoSourceType SourceType,
    string VideoUrl,
    string? ThumbnailUrl,
    int DurationSeconds,
    int SortOrder,
    bool IsFree,
    bool IsDownloadable,
    Guid? LinkedCategoryId,
    bool IsActive);
