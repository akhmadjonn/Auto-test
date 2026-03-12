using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Features.Auth;
using AutoTest.Application.Tests.TestHelpers;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace AutoTest.Application.Tests.Integration;

/// <summary>
/// Integration test: Send OTP → Verify OTP → Get Current User
/// Tests the full authentication flow across multiple handlers sharing the same DbContext.
/// </summary>
public class AuthFlowIntegrationTests
{
    private readonly IOtpService _otpService = Substitute.For<IOtpService>();
    private readonly ISmsService _smsService = Substitute.For<ISmsService>();
    private readonly IJwtTokenService _jwtService = Substitute.For<IJwtTokenService>();
    private readonly FakeDateTimeProvider _dateTime = new() { UtcNow = DateTimeOffset.UtcNow };
    private readonly FakeCurrentUser _currentUser = new();

    [Fact]
    public async Task FullAuthFlow_SendOtp_VerifyOtp_GetCurrentUser()
    {
        using var db = TestDbContextFactory.Create();
        var phone = "+998901234567";
        var code = "123456";

        // --- Step 1: Send OTP ---
        _otpService.IsRateLimitedAsync(phone, Arg.Any<CancellationToken>()).Returns(false);
        _otpService.IsOnCooldownAsync(phone, Arg.Any<CancellationToken>()).Returns(false);
        _otpService.GenerateAndStoreAsync(phone, Arg.Any<CancellationToken>()).Returns(code);

        var sendHandler = new SendOtpCommandHandler(
            _otpService, _smsService,
            Substitute.For<ILogger<SendOtpCommandHandler>>());

        var sendResult = await sendHandler.Handle(new SendOtpCommand(phone), CancellationToken.None);

        sendResult.Success.Should().BeTrue();
        await _smsService.Received(1).SendAsync(phone, Arg.Is<string>(s => s.Contains(code)), Arg.Any<CancellationToken>());

        // --- Step 2: Verify OTP (creates new user) ---
        _otpService.VerifyAsync(phone, code, Arg.Any<CancellationToken>()).Returns(true);
        _jwtService.GenerateAccessToken(Arg.Any<User>()).Returns("test-access-token");
        _jwtService.GenerateRefreshTokenAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns("test-refresh-token");

        var verifyHandler = new VerifyOtpCommandHandler(
            _otpService, _jwtService, db, _dateTime,
            Substitute.For<ILogger<VerifyOtpCommandHandler>>());

        var verifyResult = await verifyHandler.Handle(new VerifyOtpCommand(phone, code), CancellationToken.None);

        verifyResult.Success.Should().BeTrue();
        verifyResult.Data!.AccessToken.Should().Be("test-access-token");
        verifyResult.Data.RefreshToken.Should().Be("test-refresh-token");
        verifyResult.Data.IsNewUser.Should().BeTrue();

        // Verify user was created in DB
        var createdUser = await db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phone);
        createdUser.Should().NotBeNull();
        createdUser!.Role.Should().Be(UserRole.User);
        createdUser.AuthProvider.Should().Be(AuthProvider.Phone);

        // --- Step 3: Get Current User (simulating authenticated request) ---
        _currentUser.UserId = createdUser.Id;

        var getCurrentUserHandler = new GetCurrentUserQueryHandler(_currentUser, db);
        var meResult = await getCurrentUserHandler.Handle(new GetCurrentUserQuery(), CancellationToken.None);

        meResult.Success.Should().BeTrue();
        meResult.Data!.Id.Should().Be(createdUser.Id);
        meResult.Data.PhoneNumber.Should().Be(phone);
        meResult.Data.Role.Should().Be(UserRole.User);
        meResult.Data.HasActiveSubscription.Should().BeFalse();
    }

    [Fact]
    public async Task FullAuthFlow_RateLimited_CannotSendOtp()
    {
        var phone = "+998901234567";

        _otpService.IsRateLimitedAsync(phone, Arg.Any<CancellationToken>()).Returns(true);

        var sendHandler = new SendOtpCommandHandler(
            _otpService, _smsService,
            Substitute.For<ILogger<SendOtpCommandHandler>>());

        var result = await sendHandler.Handle(new SendOtpCommand(phone), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be("OTP_RATE_LIMITED");
        await _smsService.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FullAuthFlow_Cooldown_CannotSendOtp()
    {
        var phone = "+998901234567";

        _otpService.IsRateLimitedAsync(phone, Arg.Any<CancellationToken>()).Returns(false);
        _otpService.IsOnCooldownAsync(phone, Arg.Any<CancellationToken>()).Returns(true);

        var sendHandler = new SendOtpCommandHandler(
            _otpService, _smsService,
            Substitute.For<ILogger<SendOtpCommandHandler>>());

        var result = await sendHandler.Handle(new SendOtpCommand(phone), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be("OTP_COOLDOWN");
    }

    [Fact]
    public async Task FullAuthFlow_ExistingUser_ReturnsIsNewFalse()
    {
        using var db = TestDbContextFactory.Create();
        var phone = "+998901111111";

        // Pre-seed existing user
        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            PhoneNumber = phone,
            FirstName = "Ahmadjon",
            LastName = "Sirozhiddinov",
            Role = UserRole.User,
            AuthProvider = AuthProvider.Phone,
            CreatedAt = _dateTime.UtcNow
        };
        db.Users.Add(existingUser);
        await db.SaveChangesAsync();

        // Verify OTP for existing user
        _otpService.VerifyAsync(phone, "111111", Arg.Any<CancellationToken>()).Returns(true);
        _jwtService.GenerateAccessToken(Arg.Any<User>()).Returns("token");
        _jwtService.GenerateRefreshTokenAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns("refresh");

        var verifyHandler = new VerifyOtpCommandHandler(
            _otpService, _jwtService, db, _dateTime,
            Substitute.For<ILogger<VerifyOtpCommandHandler>>());

        var result = await verifyHandler.Handle(new VerifyOtpCommand(phone, "111111"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.IsNewUser.Should().BeFalse();

        // User should still exist with original details
        _currentUser.UserId = existingUser.Id;
        var meHandler = new GetCurrentUserQueryHandler(_currentUser, db);
        var meResult = await meHandler.Handle(new GetCurrentUserQuery(), CancellationToken.None);

        meResult.Data!.FirstName.Should().Be("Ahmadjon");
        meResult.Data.LastName.Should().Be("Sirozhiddinov");
    }

    [Fact]
    public async Task FullAuthFlow_GetCurrentUser_Unauthenticated_Fails()
    {
        using var db = TestDbContextFactory.Create();
        _currentUser.UserId = null;

        var handler = new GetCurrentUserQueryHandler(_currentUser, db);
        var result = await handler.Handle(new GetCurrentUserQuery(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be("UNAUTHORIZED");
    }
}
