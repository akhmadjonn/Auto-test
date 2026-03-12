using AutoTest.Application.Features.Practice;
using AutoTest.Domain.Common.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoTest.Api.Controllers;

[ApiController]
[Route("api/practice")]
[Authorize]
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

    [HttpPost("answer")]
    public async Task<IActionResult> SubmitAnswer(
        [FromBody] SubmitPracticeAnswerCommand command,
        CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
