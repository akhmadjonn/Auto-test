using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Features.Auth;
using AutoTest.Application.Tests.TestHelpers;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace AutoTest.Application.Tests.Features.Auth;

public class VerifyOtpCommandTests
{
    private readonly IOtpService _otpService = Substitute.For<IOtpService>();
    private readonly IJwtTokenService _jwtService = Substitute.For<IJwtTokenService>();
    private readonly FakeDateTimeProvider _dateTime = new();
    private readonly ILogger<VerifyOtpCommandHandler> _logger = Substitute.For<ILogger<VerifyOtpCommandHandler>>();

    public VerifyOtpCommandTests()
    {
        // Default: allow verify attempts (brute-force protection passes)
        _otpService.CheckAndIncrementVerifyAttemptsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((true, 5));
    }

    private VerifyOtpCommandHandler CreateHandler(IApplicationDbContext db) =>
        new(_otpService, _jwtService, db, _dateTime, _logger);

    [Fact]
    public async Task Handle_ValidOtp_ReturnsTokens()
    {
        using var db = TestDbContextFactory.Create();
        _otpService.VerifyAsync("+998901234567", "123456", Arg.Any<CancellationToken>()).Returns(true);
        _jwtService.GenerateAccessToken(Arg.Any<User>()).Returns("access-token");
        _jwtService.GenerateRefreshTokenAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns("refresh-token");

        var handler = CreateHandler(db);
        var result = await handler.Handle(new VerifyOtpCommand("+998901234567", "123456"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.AccessToken.Should().Be("access-token");
        result.Data.RefreshToken.Should().Be("refresh-token");
        result.Data.IsNewUser.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ValidOtp_ExistingUser_ReturnsIsNewFalse()
    {
        using var db = TestDbContextFactory.Create();
        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            PhoneNumber = "+998901234567",
            Role = UserRole.User,
            AuthProvider = AuthProvider.Phone,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(existingUser);
        await db.SaveChangesAsync();

        _otpService.VerifyAsync("+998901234567", "123456", Arg.Any<CancellationToken>()).Returns(true);
        _jwtService.GenerateAccessToken(Arg.Any<User>()).Returns("access-token");
        _jwtService.GenerateRefreshTokenAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns("refresh-token");

        var handler = CreateHandler(db);
        var result = await handler.Handle(new VerifyOtpCommand("+998901234567", "123456"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.IsNewUser.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ExpiredOtp_ReturnsFail()
    {
        using var db = TestDbContextFactory.Create();
        _otpService.VerifyAsync("+998901234567", "123456", Arg.Any<CancellationToken>()).Returns(false);

        var handler = CreateHandler(db);
        var result = await handler.Handle(new VerifyOtpCommand("+998901234567", "123456"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be("OTP_INVALID");
    }

    [Fact]
    public async Task Handle_WrongCode_ReturnsFail()
    {
        using var db = TestDbContextFactory.Create();
        _otpService.VerifyAsync("+998901234567", "000000", Arg.Any<CancellationToken>()).Returns(false);

        var handler = CreateHandler(db);
        var result = await handler.Handle(new VerifyOtpCommand("+998901234567", "000000"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be("OTP_INVALID");
    }

    [Fact]
    public void Validator_EmptyPhone_Invalid()
    {
        var validator = new VerifyOtpCommandValidator();
        var result = validator.Validate(new VerifyOtpCommand("", "123456"));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_NonDigitCode_Invalid()
    {
        var validator = new VerifyOtpCommandValidator();
        var result = validator.Validate(new VerifyOtpCommand("+998901234567", "abcdef"));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_ShortCode_Invalid()
    {
        var validator = new VerifyOtpCommandValidator();
        var result = validator.Validate(new VerifyOtpCommand("+998901234567", "123"));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_TooManyAttempts_ReturnsRateLimited()
    {
        using var db = TestDbContextFactory.Create();
        _otpService.CheckAndIncrementVerifyAttemptsAsync("+998901234567", Arg.Any<CancellationToken>())
            .Returns((false, 0));

        var handler = CreateHandler(db);
        var result = await handler.Handle(new VerifyOtpCommand("+998901234567", "123456"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be("OTP_TOO_MANY_ATTEMPTS");
        // VerifyAsync should NOT be called when rate limited
        await _otpService.DidNotReceive().VerifyAsync("+998901234567", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AttemptsResetOnSuccess()
    {
        using var db = TestDbContextFactory.Create();
        _otpService.CheckAndIncrementVerifyAttemptsAsync("+998901234567", Arg.Any<CancellationToken>())
            .Returns((true, 4));
        _otpService.VerifyAsync("+998901234567", "123456", Arg.Any<CancellationToken>()).Returns(true);
        _jwtService.GenerateAccessToken(Arg.Any<User>()).Returns("access-token");
        _jwtService.GenerateRefreshTokenAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns("refresh-token");

        var handler = CreateHandler(db);
        var result = await handler.Handle(new VerifyOtpCommand("+998901234567", "123456"), CancellationToken.None);

        result.Success.Should().BeTrue();
        // Verify attempt counter should be reset after successful OTP
        await _otpService.Received(1).ResetVerifyAttemptsAsync("+998901234567", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WrongCode_ShowsRemainingAttempts()
    {
        using var db = TestDbContextFactory.Create();
        _otpService.CheckAndIncrementVerifyAttemptsAsync("+998901234567", Arg.Any<CancellationToken>())
            .Returns((true, 3));
        _otpService.VerifyAsync("+998901234567", "000000", Arg.Any<CancellationToken>()).Returns(false);

        var handler = CreateHandler(db);
        var result = await handler.Handle(new VerifyOtpCommand("+998901234567", "000000"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be("OTP_INVALID");
        result.Error.Message.Should().Contain("3 attempts remaining");
    }
}
