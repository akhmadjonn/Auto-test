using AutoTest.Application.Common.Constants;
using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Auth;

public record SendOtpCommand(string PhoneNumber, string? Language = null) : IRequest<ApiResponse>;

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
        var phone = request.PhoneNumber.TrimStart('+');

        if (await otpService.IsRateLimitedAsync(phone, ct))
        {
            logger.LogWarning("OTP rate limit hit for {Phone}", phone);
            return ApiResponse.Fail("OTP_RATE_LIMITED", "Too many OTP requests. Please wait 15 minutes.");
        }

        if (await otpService.IsOnCooldownAsync(phone, ct))
            return ApiResponse.Fail("OTP_COOLDOWN", "Please wait 60 seconds before requesting another OTP.");

        var code = await otpService.GenerateAndStoreAsync(phone, ct);

        if (otpService.IsWhitelistedNumber(phone))
        {
            logger.LogInformation("Whitelist OTP for {Phone}", phone);
            return ApiResponse.Ok();
        }

        var language = ParseLanguage(request.Language);
        var message = SmsTemplates.FormatOtp(code, language, otpService.GetAndroidAppHash());

        try
        {
            await smsService.SendAsync(phone, message, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SMS send failed for {Phone} — OTP still valid in Redis", phone);
        }

        logger.LogInformation("OTP sent to {Phone} lang={Language}", phone, language);
        return ApiResponse.Ok();
    }

    private static Language ParseLanguage(string? lang) => lang?.ToLowerInvariant() switch
    {
        "uz" => Language.Uz,
        "uzlatin" => Language.UzLatin,
        "ru" => Language.Ru,
        _ => Language.UzLatin
    };
}
