using AutoTest.Api.Services;
using AutoTest.Application.Common.Models;
using AutoTest.Application.Features.Diagnostics;
using AutoTest.Domain.Enum;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AutoTest.Api.Controllers;

// Lightweight verification endpoints for the localized BaseResponse system.
// Public, read-only — no business state is mutated. Useful for e2e tests
// that need to assert X-Api-Lang routing without touching real handlers.
[ApiController]
[Route("api/v1/diagnostics")]
public class DiagnosticsController(IResponseService responseService, IMediator mediator) : ControllerBase
{
    // GET /api/v1/diagnostics/throw → handler throws → ExceptionHandlingBehavior should convert
    // into BaseResponse.Error(ErrorWhileProcess) → ResponseService localizes → HTTP 500.
    [HttpGet("throw")]
    public async Task<IActionResult> Throw(CancellationToken ct)
        => responseService.GetResponse(await mediator.Send(new DiagnosticsThrowCommand(), ct));

    // POST /api/v1/diagnostics/validate → ValidationBehavior fails → ExceptionHandlingBehavior
    // catches ValidationException → BaseResponse.Error(ValidationError, "<concrete msgs>") → HTTP 400.
    // Concrete validator messages must be PRESERVED (not overwritten by catalog) per spec.
    [HttpPost("validate")]
    public async Task<IActionResult> Validate([FromBody] DiagnosticsValidateCommand cmd, CancellationToken ct)
        => responseService.GetResponse(await mediator.Send(cmd, ct));

    // GET /api/v1/diagnostics/ping → BaseResponse success (errorCode=0, httpStatusCode=200)
    [HttpGet("ping")]
    public IActionResult Ping()
        => responseService.GetResponse(new BaseResponse<string>().Success("pong"));

    // GET /api/v1/diagnostics/error/{code} → BaseResponse error with localized message
    // Example: /api/v1/diagnostics/error/-32010 with X-Api-Lang: ru → "Пользователь не найден."
    [HttpGet("error/{code:int}")]
    public IActionResult Error(int code)
        => responseService.GetResponse(new BaseResponse<object>().Error(code));

    // GET /api/v1/diagnostics/error-by-name/{name} → resolve by enum name
    [HttpGet("error-by-name/{name}")]
    public IActionResult ErrorByName(string name)
    {
        if (!System.Enum.TryParse<ErrorEnum>(name, ignoreCase: true, out var parsed))
            return responseService.GetResponse(
                new BaseResponse<object>().Error((int)ErrorEnum.ArgumentError));

        return responseService.GetResponse(new BaseResponse<object>().Error((int)parsed));
    }
}
