using AutoTest.Application.Features.ColorVision;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutoTest.Api.Controllers;

[ApiController]
[Route("api/v1/admin/color-vision/plates")]
[Authorize(Roles = "Admin")]
[EnableRateLimiting("authenticated")]
public class AdminColorVisionController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await mediator.Send(new GetAdminColorVisionPlatesQuery(), ct);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateColorVisionPlateCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateColorVisionPlateBody body, CancellationToken ct)
    {
        var command = new UpdateColorVisionPlateCommand(
            id, body.PlateNumber, body.ExpectedAnswer, body.AlternateAnswer,
            body.SortOrder, body.IsActive);
        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteColorVisionPlateCommand(id), ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost("{id}/image")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile image, CancellationToken ct)
    {
        var command = new UploadColorVisionPlateImageCommand(id, image.OpenReadStream(), image.FileName);
        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }
}

public record UpdateColorVisionPlateBody(
    int PlateNumber,
    string ExpectedAnswer,
    string? AlternateAnswer,
    int SortOrder,
    bool IsActive);
