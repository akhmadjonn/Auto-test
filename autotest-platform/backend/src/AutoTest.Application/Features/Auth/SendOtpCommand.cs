using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Auth;

public record SendOtpCommand(string PhoneNumber) : IRequest<ApiResponse>;

public class SendOtpCommandValidator : AbstractValidator<SendOtpCommand>
{
    public SendOtpCommandValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty()
            .Matches(@"^\+?[0-9]{9,15}$")
            .WithMessage("Invalid phone number format");
    }
}

public class SendOtpCommandHandler(
    IOtpService otpService,
    ISmsService smsService,
    ILogger<SendOtpCommandHandler> logger) : IRequestHandler<SendOtpCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(SendOtpCommand request, CancellationToken ct)
    {
        if (await otpService.IsRateLimitedAsync(request.PhoneNumber, ct))
        {
            logger.LogWarning("OTP rate limit hit for {Phone}", request.PhoneNumber);
            return ApiResponse.Fail("OTP_RATE_LIMITED", "Too many OTP requests. Please wait 15 minutes.");
        }

        if (await otpService.IsOnCooldownAsync(request.PhoneNumber, ct))
            return ApiResponse.Fail("OTP_COOLDOWN", "Please wait 60 seconds before requesting another OTP.");

        var code = await otpService.GenerateAndStoreAsync(request.PhoneNumber, ct);
        await smsService.SendAsync(request.PhoneNumber, $"Avtolider: your code is {code}. Valid for 5 minutes.", ct);

        logger.LogInformation("OTP sent to {Phone}", request.PhoneNumber);
        return ApiResponse.Ok();
    }
}
