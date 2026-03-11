using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using FluentValidation;
using MediatR;

namespace AutoTest.Application.Features.Auth;

public record LogoutCommand(string RefreshToken) : IRequest<ApiResponse>;

public class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

public class LogoutCommandHandler(IJwtTokenService jwtService) : IRequestHandler<LogoutCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(LogoutCommand request, CancellationToken ct)
    {
        await jwtService.RevokeRefreshTokenAsync(request.RefreshToken, ct);
        return ApiResponse.Ok();
    }
}
