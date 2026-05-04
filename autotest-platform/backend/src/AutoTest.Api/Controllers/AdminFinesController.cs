using AutoTest.Application.Features.TrafficFines;
using AutoTest.Domain.Common.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutoTest.Api.Controllers;

[ApiController]
[Route("api/v1/admin/fines")]
[Authorize(Roles = "Admin")]
[EnableRateLimiting("authenticated")]
public class AdminFinesController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFineCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateFineBody body, CancellationToken ct)
    {
        var command = new UpdateFineCommand(
            id, body.ArticleNumber, body.ViolationDescription, body.AdditionalNotes,
            body.PenaltyAmountTiyins, body.PenaltyMaxTiyins, body.SortOrder);
        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteFineCommand(id), ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost("{id:guid}/image")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile image, CancellationToken ct)
    {
        var command = new UploadFineImageCommand(id, image.OpenReadStream(), image.FileName);
        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }
}

public record UpdateFineBody(
    string ArticleNumber,
    LocalizedText ViolationDescription,
    LocalizedText? AdditionalNotes,
    long PenaltyAmountTiyins,
    long? PenaltyMaxTiyins,
    int SortOrder);
