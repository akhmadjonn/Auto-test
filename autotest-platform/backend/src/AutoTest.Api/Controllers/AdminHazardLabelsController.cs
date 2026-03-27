using AutoTest.Application.Common.Models;
using AutoTest.Application.Features.HazardLabels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutoTest.Api.Controllers;

[ApiController]
[Route("api/v1/admin/hazard-labels")]
[Authorize(Roles = "Admin")]
[EnableRateLimiting("authenticated")]
public class AdminHazardLabelsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateHazardLabelCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return result.Success ? CreatedAtAction(nameof(Create), result) : BadRequest(result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateHazardLabelCommand command, CancellationToken ct)
    {
        if (id != command.Id)
            return BadRequest(ApiResponse.Fail("ID_MISMATCH", "Route ID does not match body ID."));

        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteHazardLabelCommand(id), ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost("{id}/image")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile image, CancellationToken ct)
    {
        var command = new UploadHazardLabelImageCommand(id, image.OpenReadStream(), image.FileName);
        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
