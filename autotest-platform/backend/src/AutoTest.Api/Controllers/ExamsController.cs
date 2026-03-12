using AutoTest.Application.Features.Exams;
using AutoTest.Domain.Common.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutoTest.Api.Controllers;

[ApiController]
[Route("api/v1/exams")]
[Authorize]
[EnableRateLimiting("authenticated")]
public class ExamsController(IMediator mediator) : ControllerBase
{
    [HttpPost("start")]
    public async Task<IActionResult> Start([FromBody] StartExamCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("start-ticket")]
    public async Task<IActionResult> StartTicket([FromBody] StartTicketExamCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("start-marathon")]
    public async Task<IActionResult> StartMarathon([FromBody] StartMarathonCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{sessionId}/answer")]
    public async Task<IActionResult> SubmitAnswer(Guid sessionId, [FromBody] SubmitAnswerRequest req, CancellationToken ct)
    {
        var command = new SubmitAnswerCommand(sessionId, req.SessionQuestionId, req.SelectedAnswerId, req.TimeSpentSeconds);
        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{sessionId}/complete")]
    public async Task<IActionResult> Complete(Guid sessionId, [FromQuery] Language language = Language.UzLatin, CancellationToken ct = default)
    {
        var result = await mediator.Send(new CompleteExamCommand(sessionId, language), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{sessionId}")]
    public async Task<IActionResult> GetSession(Guid sessionId, [FromQuery] Language language = Language.UzLatin, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetExamSessionQuery(sessionId, language), ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpGet("{sessionId}/result")]
    public async Task<IActionResult> GetResult(Guid sessionId, [FromQuery] Language language = Language.UzLatin, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetExamResultQuery(sessionId, language), ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetExamHistoryQuery(page, pageSize), ct);
        return Ok(result);
    }
}

public record SubmitAnswerRequest(Guid SessionQuestionId, Guid SelectedAnswerId, int? TimeSpentSeconds);
