using AutoTest.Application.Common.Models;
using AutoTest.Application.Features.Glossary;
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
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] UpdateGlossaryCategoryCommand command, CancellationToken ct)
    {
        if (id != command.Id)
            return BadRequest(ApiResponse.Fail("ID_MISMATCH", "Route ID does not match body ID."));

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
    public async Task<IActionResult> UpdateTerm(Guid id, [FromBody] UpdateGlossaryTermCommand command, CancellationToken ct)
    {
        if (id != command.Id)
            return BadRequest(ApiResponse.Fail("ID_MISMATCH", "Route ID does not match body ID."));

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
