using AutoTest.Application.Common.Models;
using AutoTest.Application.Features.FirstAid;
using AutoTest.Domain.Common.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutoTest.Api.Controllers;

[ApiController]
[Route("api/v1/admin/first-aid")]
[Authorize(Roles = "Admin")]
[EnableRateLimiting("authenticated")]
public class AdminFirstAidController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFirstAidProcedureCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateFirstAidProcedureBody body, CancellationToken ct)
    {
        // Id always comes from the route — frontend never puts it in the body.
        var command = new UpdateFirstAidProcedureCommand(id, body.Slug, body.Name, body.Summary, body.SortOrder);
        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteFirstAidProcedureCommand(id), ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost("{procedureId}/steps")]
    public async Task<IActionResult> CreateStep(Guid procedureId, [FromBody] CreateFirstAidStepBody body, CancellationToken ct)
    {
        var command = new CreateFirstAidStepCommand(procedureId, body.StepOrder, body.Title, body.Description);
        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("{procedureId}/steps/{stepId}")]
    public async Task<IActionResult> UpdateStep(Guid procedureId, Guid stepId, [FromBody] UpdateFirstAidStepBody body, CancellationToken ct)
    {
        var command = new UpdateFirstAidStepCommand(procedureId, stepId, body.StepOrder, body.Title, body.Description);
        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{procedureId}/steps/{stepId}")]
    public async Task<IActionResult> DeleteStep(Guid procedureId, Guid stepId, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteFirstAidStepCommand(procedureId, stepId), ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost("{procedureId}/steps/{stepId}/image")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadStepImage(Guid procedureId, Guid stepId, IFormFile image, CancellationToken ct)
    {
        var command = new UploadFirstAidStepImageCommand(
            procedureId, stepId,
            image.OpenReadStream(),
            image.FileName);

        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id}/icon")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadIcon(Guid id, IFormFile image, CancellationToken ct)
    {
        var command = new UploadFirstAidProcedureIconCommand(id, image.OpenReadStream(), image.FileName);
        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }
}

// Request bodies — IDs come from the route, not the body. The frontend
// never sends `id` so the binding stays simple (matches its actual JSON).
public record UpdateFirstAidProcedureBody(
    string Slug,
    LocalizedText Name,
    LocalizedText? Summary,
    int SortOrder);

public record CreateFirstAidStepBody(
    int StepOrder,
    LocalizedText Title,
    LocalizedText Description);

public record UpdateFirstAidStepBody(
    int StepOrder,
    LocalizedText Title,
    LocalizedText Description);
