using AutoTest.Application.Features.Glossary;
using AutoTest.Domain.Common.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutoTest.Api.Controllers;

[ApiController]
[Route("api/v1/admin/glossary")]
[Authorize(Roles = "Admin")]
[EnableRateLimiting("authenticated")]
public class AdminGlossaryController(IMediator mediator) : ControllerBase
{
    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory([FromBody] CreateGlossaryCategoryCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return result.Success ? CreatedAtAction(nameof(CreateCategory), result) : BadRequest(result);
    }

    [HttpPut("categories/{id}")]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] UpdateGlossaryCategoryBody body, CancellationToken ct)
    {
        var command = new UpdateGlossaryCategoryCommand(id, body.Name, body.Slug, body.Icon, body.SortOrder);
        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpDelete("categories/{id}")]
    public async Task<IActionResult> DeleteCategory(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteGlossaryCategoryCommand(id), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("terms")]
    public async Task<IActionResult> CreateTerm([FromBody] CreateGlossaryTermCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return result.Success ? CreatedAtAction(nameof(CreateTerm), result) : BadRequest(result);
    }

    [HttpPut("terms/{id}")]
    public async Task<IActionResult> UpdateTerm(Guid id, [FromBody] UpdateGlossaryTermBody body, CancellationToken ct)
    {
        var command = new UpdateGlossaryTermCommand(
            id, body.GlossaryCategoryId, body.Term, body.Definition,
            body.SortOrder, body.RelatedQuestionIds);
        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpDelete("terms/{id}")]
    public async Task<IActionResult> DeleteTerm(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteGlossaryTermCommand(id), ct);
        return result.Success ? Ok(result) : NotFound(result);
    }
}

public record UpdateGlossaryCategoryBody(
    LocalizedText Name,
    string Slug,
    string? Icon,
    int SortOrder);

public record UpdateGlossaryTermBody(
    Guid GlossaryCategoryId,
    LocalizedText Term,
    LocalizedText Definition,
    int SortOrder,
    Guid[]? RelatedQuestionIds);
