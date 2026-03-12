using AutoTest.Application.Features.Questions;
using AutoTest.Domain.Common.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutoTest.Api.Controllers;

[ApiController]
[Route("api/v1/questions")]
[Authorize]
[EnableRateLimiting("authenticated")]
public class QuestionsController(IMediator mediator) : ControllerBase
{
    [HttpGet("category/{categoryId}")]
    public async Task<IActionResult> GetByCategory(
        Guid categoryId,
        [FromQuery] Language language = Language.UzLatin,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Difficulty? difficulty = null,
        [FromQuery] LicenseCategory? licenseCategory = null,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new GetQuestionsByCategoryQuery(categoryId, language, page, pageSize, difficulty, licenseCategory), ct);
        return Ok(result);
    }

    [HttpGet("tickets")]
    public async Task<IActionResult> GetTickets(
        [FromQuery] LicenseCategory? licenseCategory = null,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetTicketsListQuery(licenseCategory), ct);
        return Ok(result);
    }

    [HttpGet("tickets/{number:int}")]
    public async Task<IActionResult> GetByTicket(
        int number,
        [FromQuery] Language language = Language.UzLatin,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetQuestionsByTicketQuery(number, language), ct);
        return Ok(result);
    }
}
