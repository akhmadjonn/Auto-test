using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Auth;

public record VerifyOtpCommand(string PhoneNumber, string Code) : IRequest<ApiResponse<AuthTokensDto>>;

public record AuthTokensDto(string AccessToken, string RefreshToken, bool IsNewUser);

public class VerifyOtpCommandValidator : AbstractValidator<VerifyOtpCommand>
{
    public VerifyOtpCommandValidator()
    {
        RuleFor(x => x.PhoneNumber).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().Length(6).Matches(@"^\d{6}$");
    }
}

public class VerifyOtpCommandHandler(
    IOtpService otpService,
    IJwtTokenService jwtService,
    IApplicationDbContext db,
    IDateTimeProvider dateTime,
    ILogger<VerifyOtpCommandHandler> logger) : IRequestHandler<VerifyOtpCommand, ApiResponse<AuthTokensDto>>
{
    public async Task<ApiResponse<AuthTokensDto>> Handle(VerifyOtpCommand request, CancellationToken ct)
    {
        // Brute-force protection: limit verify attempts per phone number
        var (allowed, remaining) = await otpService.CheckAndIncrementVerifyAttemptsAsync(request.PhoneNumber, ct);
        if (!allowed)
            return ApiResponse<AuthTokensDto>.Fail("OTP_TOO_MANY_ATTEMPTS", "Too many attempts. Try again in 15 minutes.");

        var valid = await otpService.VerifyAsync(request.PhoneNumber, request.Code, ct);
        if (!valid)
            return ApiResponse<AuthTokensDto>.Fail("OTP_INVALID", $"Invalid or expired OTP code. {remaining} attempts remaining.");

        // Successful verify — reset attempt counter
        await otpService.ResetVerifyAttemptsAsync(request.PhoneNumber, ct);

        var isNew = false;
        var user = await db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber, ct);

        if (user is null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                PhoneNumber = request.PhoneNumber,
                Role = UserRole.User,
                AuthProvider = AuthProvider.Phone,
                PreferredLanguage = Language.UzLatin,
                CreatedAt = dateTime.UtcNow,
                UpdatedAt = dateTime.UtcNow
            };
            db.Users.Add(user);
            isNew = true;
        }

        user.LastActiveAt = dateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var accessToken = jwtService.GenerateAccessToken(user);
        var refreshToken = await jwtService.GenerateRefreshTokenAsync(user.Id, ct);

        logger.LogInformation("User {UserId} authenticated via OTP (new: {IsNew})", user.Id, isNew);
        return ApiResponse<AuthTokensDto>.Ok(new AuthTokensDto(accessToken, refreshToken, isNew));
    }
}
