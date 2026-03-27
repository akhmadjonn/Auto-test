using AutoTest.Application.Features.Practice;
using AutoTest.Domain.Common.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutoTest.Api.Controllers;

[ApiController]
[Route("api/v1/practice")]
[Authorize]
[EnableRateLimiting("authenticated")]
public class PracticeController(ISender mediator) : ControllerBase
{
    [HttpGet("session")]
    public async Task<IActionResult> GetSession(
        [FromQuery] Guid? categoryId,
        [FromQuery] Language language = Language.UzLatin,
        [FromQuery] int batchSize = 10,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new GetPracticeSessionQuery(categoryId, language, batchSize), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("due-count")]
    public async Task<IActionResult> GetDueCount(
        [FromQuery] Guid? categoryId,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetDueReviewCountQuery(categoryId), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("review")]
    public async Task<IActionResult> GetReviewQuestions(
        [FromQuery] int limit = 20,
        [FromQuery] Language language = Language.UzLatin,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetQuestionsForReviewQuery(limit, language), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("hard")]
    public async Task<IActionResult> GetHardQuestions(
        [FromQuery] int limit = 20,
        [FromQuery] Language language = Language.UzLatin,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetHardQuestionsQuery(limit, language), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("answer")]
    public async Task<IActionResult> SubmitAnswer(
        [FromBody] SubmitPracticeAnswerCommand command,
        CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
