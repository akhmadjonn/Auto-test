using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using MediatR;

namespace AutoTest.Application.Features.Auth;

public record LogoutCommand : IRequest<ApiResponse>;

public class LogoutCommandHandler(IJwtTokenService jwtService, ICurrentUser currentUser)
    : IRequestHandler<LogoutCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(LogoutCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is { } userId && currentUser.SessionId is { } sessionId)
            await jwtService.RevokeSessionAsync(userId, sessionId, ct);

        return ApiResponse.Ok();
    }
}
