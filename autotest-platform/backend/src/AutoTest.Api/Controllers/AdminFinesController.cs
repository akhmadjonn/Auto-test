using AutoTest.Application.Features.TrafficFines;
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
    public async Task<IActionResult> Create([FromBody] CreateFineRequest request, CancellationToken ct)
    {
        var command = new CreateFineCommand(
            request.ArticleNumber,
            request.ViolationDescriptionUz,
            request.ViolationDescriptionUzLatin,
            request.ViolationDescriptionRu,
            request.AdditionalNotesUz,
            request.AdditionalNotesUzLatin,
            request.AdditionalNotesRu,
            request.PenaltyAmountTiyins,
            request.PenaltyMaxTiyins,
            request.SortOrder);

        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateFineRequest request, CancellationToken ct)
    {
        var command = new UpdateFineCommand(
            id,
            request.ArticleNumber,
            request.ViolationDescriptionUz,
            request.ViolationDescriptionUzLatin,
            request.ViolationDescriptionRu,
            request.AdditionalNotesUz,
            request.AdditionalNotesUzLatin,
            request.AdditionalNotesRu,
            request.PenaltyAmountTiyins,
            request.PenaltyMaxTiyins,
            request.SortOrder);

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

public record CreateFineRequest(
    string ArticleNumber,
    string ViolationDescriptionUz,
    string ViolationDescriptionUzLatin,
    string ViolationDescriptionRu,
    string? AdditionalNotesUz,
    string? AdditionalNotesUzLatin,
    string? AdditionalNotesRu,
    long PenaltyAmountTiyins,
    long? PenaltyMaxTiyins,
    int SortOrder);

public record UpdateFineRequest(
    string ArticleNumber,
    string ViolationDescriptionUz,
    string ViolationDescriptionUzLatin,
    string ViolationDescriptionRu,
    string? AdditionalNotesUz,
    string? AdditionalNotesUzLatin,
    string? AdditionalNotesRu,
    long PenaltyAmountTiyins,
    long? PenaltyMaxTiyins,
    int SortOrder);
