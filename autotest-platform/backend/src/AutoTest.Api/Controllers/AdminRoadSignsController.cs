using AutoTest.Application.Features.RoadSigns;
using AutoTest.Domain.Common.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutoTest.Api.Controllers;

[ApiController]
[Route("api/v1/admin/road-signs")]
[Authorize(Roles = "Admin")]
[EnableRateLimiting("authenticated")]
public class AdminRoadSignsController(IMediator mediator) : ControllerBase
{
    // --- Categories ---

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories(CancellationToken ct)
    {
        var result = await mediator.Send(new GetRoadSignCategoriesQuery(IncludeInactive: true), ct);
        return Ok(result);
    }

    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory([FromBody] CreateRoadSignCategoryCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return result.Success ? CreatedAtAction(nameof(CreateCategory), result) : BadRequest(result);
    }

    [HttpPut("categories/{id}")]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] UpdateRoadSignCategoryBody body, CancellationToken ct)
    {
        var command = new UpdateRoadSignCategoryCommand(
            id, body.Slug, body.Code, body.Name, body.Description,
            body.SortOrder, body.IsActive);
        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpDelete("categories/{id}")]
    public async Task<IActionResult> DeleteCategory(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteRoadSignCategoryCommand(id), ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost("categories/{id}/icon")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadCategoryIcon(Guid id, IFormFile image, CancellationToken ct)
    {
        var command = new UploadRoadSignCategoryIconCommand(id, image.OpenReadStream(), image.FileName);
        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    // --- Signs ---

    [HttpGet]
    public async Task<IActionResult> GetSigns([FromQuery] Guid? categoryId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetRoadSignsByCategoryQuery(categoryId, IncludeInactive: true), ct);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetSign(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetRoadSignByIdQuery(id), ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateSign([FromBody] CreateRoadSignCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return result.Success ? CreatedAtAction(nameof(CreateSign), result) : BadRequest(result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateSign(Guid id, [FromBody] UpdateRoadSignBody body, CancellationToken ct)
    {
        var command = new UpdateRoadSignCommand(
            id, body.CategoryId, body.SignCode, body.Name, body.Description,
            body.SortOrder, body.IsActive);
        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSign(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteRoadSignCommand(id), ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost("{id}/image")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadSignImage(Guid id, IFormFile image, CancellationToken ct)
    {
        var command = new UploadRoadSignImageCommand(id, image.OpenReadStream(), image.FileName);
        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }
}

public record UpdateRoadSignCategoryBody(
    string Slug,
    string Code,
    LocalizedText Name,
    LocalizedText Description,
    int SortOrder,
    bool IsActive);

public record UpdateRoadSignBody(
    Guid CategoryId,
    string SignCode,
    LocalizedText Name,
    LocalizedText? Description,
    int SortOrder,
    bool IsActive);
