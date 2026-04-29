using AutoTest.Application.Common.Models;
using FluentValidation;
using MediatR;

namespace AutoTest.Application.Features.Diagnostics;

// Diagnostic commands for verifying the MediatR pipeline behaviors:
//  - DiagnosticsThrowCommand: handler throws → ExceptionHandlingBehavior catches
//  - DiagnosticsValidateCommand: validator may fail → ExceptionHandlingBehavior maps
//    ValidationException to BaseResponse.Error(ValidationError, <concrete msgs>)

public record DiagnosticsThrowCommand : IRequest<BaseResponse<string>>;

public class DiagnosticsThrowCommandHandler : IRequestHandler<DiagnosticsThrowCommand, BaseResponse<string>>
{
    public Task<BaseResponse<string>> Handle(DiagnosticsThrowCommand request, CancellationToken ct)
        => throw new InvalidOperationException("intentional test failure");
}

public record DiagnosticsValidateCommand(string Phone) : IRequest<BaseResponse<string>>;

public class DiagnosticsValidateValidator : AbstractValidator<DiagnosticsValidateCommand>
{
    public DiagnosticsValidateValidator()
    {
        RuleFor(x => x.Phone)
            .NotEmpty()
            .Matches(@"^\+?[0-9]{9,15}$")
            .WithMessage("phone must be 9-15 digits");
    }
}

public class DiagnosticsValidateHandler : IRequestHandler<DiagnosticsValidateCommand, BaseResponse<string>>
{
    public Task<BaseResponse<string>> Handle(DiagnosticsValidateCommand request, CancellationToken ct)
        => Task.FromResult(new BaseResponse<string>().Success("phone ok: " + request.Phone));
}
