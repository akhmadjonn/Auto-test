using AutoTest.Application.Features.RoadMarkings;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutoTest.Api.Controllers;

[ApiController]
[Route("api/v1/admin/road-markings")]
[Authorize(Roles = "Admin")]
[EnableRateLimiting("authenticated")]
public class AdminRoadMarkingsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] RoadMarkingType? type, CancellationToken ct)
    {
        var result = await mediator.Send(new GetRoadMarkingsQuery(type, IncludeInactive: true), ct);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetRoadMarkingByIdQuery(id), ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRoadMarkingCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return result.Success ? CreatedAtAction(nameof(Create), result) : BadRequest(result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRoadMarkingBody body, CancellationToken ct)
    {
        var command = new UpdateRoadMarkingCommand(
            id, body.MarkingCode, body.MarkingType, body.Name, body.Description,
            body.SortOrder, body.IsActive);
        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteRoadMarkingCommand(id), ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost("{id}/image")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile image, CancellationToken ct)
    {
        var command = new UploadRoadMarkingImageCommand(id, image.OpenReadStream(), image.FileName);
        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }
}

public record UpdateRoadMarkingBody(
    string MarkingCode,
    RoadMarkingType MarkingType,
    LocalizedText Name,
    LocalizedText? Description,
    int SortOrder,
    bool IsActive);
