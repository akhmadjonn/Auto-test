using AutoTest.Application.Features.Categories;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutoTest.Api.Controllers;

[ApiController]
[Route("api/v1/admin/categories")]
[Authorize(Roles = "Admin")]
[EnableRateLimiting("authenticated")]
public class AdminCategoriesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Language language = Language.UzLatin, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetCategoriesTreeQuery(language, IncludeInactive: true), ct);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCategoryCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return result.Success ? CreatedAtAction(nameof(GetAll), result) : BadRequest(result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCategoryBody body, CancellationToken ct)
    {
        var command = new UpdateCategoryCommand(
            id, body.Name, body.Description, body.Slug, body.IconUrl,
            body.ParentId, body.SortOrder, body.IsActive);
        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }
}

public record UpdateCategoryBody(
    LocalizedText Name,
    LocalizedText Description,
    string Slug,
    string? IconUrl,
    Guid? ParentId,
    int SortOrder,
    bool IsActive);
