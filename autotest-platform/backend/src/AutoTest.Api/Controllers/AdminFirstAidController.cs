using AutoTest.Application.Features.FirstAid;
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
    public async Task<IActionResult> Create([FromBody] CreateFirstAidProcedureRequest request, CancellationToken ct)
    {
        var command = new CreateFirstAidProcedureCommand(
            request.Slug,
            request.NameUz, request.NameUzLatin, request.NameRu,
            request.SummaryUz, request.SummaryUzLatin, request.SummaryRu,
            request.SortOrder);

        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateFirstAidProcedureRequest request, CancellationToken ct)
    {
        var command = new UpdateFirstAidProcedureCommand(
            id,
            request.Slug,
            request.NameUz, request.NameUzLatin, request.NameRu,
            request.SummaryUz, request.SummaryUzLatin, request.SummaryRu,
            request.SortOrder);

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
    public async Task<IActionResult> CreateStep(Guid procedureId, [FromBody] CreateFirstAidStepRequest request, CancellationToken ct)
    {
        var command = new CreateFirstAidStepCommand(
            procedureId,
            request.StepOrder,
            request.TitleUz, request.TitleUzLatin, request.TitleRu,
            request.DescriptionUz, request.DescriptionUzLatin, request.DescriptionRu);

        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("{procedureId}/steps/{stepId}")]
    public async Task<IActionResult> UpdateStep(Guid procedureId, Guid stepId, [FromBody] UpdateFirstAidStepRequest request, CancellationToken ct)
    {
        var command = new UpdateFirstAidStepCommand(
            procedureId, stepId,
            request.StepOrder,
            request.TitleUz, request.TitleUzLatin, request.TitleRu,
            request.DescriptionUz, request.DescriptionUzLatin, request.DescriptionRu);

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
}

// Request models
public record CreateFirstAidProcedureRequest(
    string Slug,
    string NameUz,
    string NameUzLatin,
    string NameRu,
    string? SummaryUz,
    string? SummaryUzLatin,
    string? SummaryRu,
    int SortOrder);

public record UpdateFirstAidProcedureRequest(
    string Slug,
    string NameUz,
    string NameUzLatin,
    string NameRu,
    string? SummaryUz,
    string? SummaryUzLatin,
    string? SummaryRu,
    int SortOrder);

public record CreateFirstAidStepRequest(
    int StepOrder,
    string TitleUz,
    string TitleUzLatin,
    string TitleRu,
    string DescriptionUz,
    string DescriptionUzLatin,
    string DescriptionRu);

public record UpdateFirstAidStepRequest(
    int StepOrder,
    string TitleUz,
    string TitleUzLatin,
    string TitleRu,
    string DescriptionUz,
    string DescriptionUzLatin,
    string DescriptionRu);
