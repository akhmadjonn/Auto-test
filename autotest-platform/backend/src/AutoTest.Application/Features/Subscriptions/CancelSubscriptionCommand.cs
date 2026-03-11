using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Subscriptions;

public record CancelSubscriptionCommand(Guid SubscriptionId) : IRequest<ApiResponse<CancelSubscriptionResultDto>>;

public record CancelSubscriptionResultDto(Guid SubscriptionId, bool AutoRenewDisabled, DateTimeOffset ExpiresAt);

public class CancelSubscriptionCommandValidator : AbstractValidator<CancelSubscriptionCommand>
{
    public CancelSubscriptionCommandValidator()
    {
        RuleFor(x => x.SubscriptionId).NotEmpty();
    }
}

public class CancelSubscriptionCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider dateTime,
    ILogger<CancelSubscriptionCommandHandler> logger)
    : IRequestHandler<CancelSubscriptionCommand, ApiResponse<CancelSubscriptionResultDto>>
{
    public async Task<ApiResponse<CancelSubscriptionResultDto>> Handle(
        CancelSubscriptionCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return ApiResponse<CancelSubscriptionResultDto>.Fail("UNAUTHORIZED", "Not authenticated.");

        var userId = currentUser.UserId.Value;
        var now = dateTime.UtcNow;

        var subscription = await db.Subscriptions
            .FirstOrDefaultAsync(s => s.Id == request.SubscriptionId
                && s.UserId == userId
                && s.Status == SubscriptionStatus.Active, ct);

        if (subscription is null)
            return ApiResponse<CancelSubscriptionResultDto>.Fail(
                "SUBSCRIPTION_NOT_FOUND", "Active subscription not found.");

        // Disable auto-renew; keep access until period ends
        subscription.AutoRenew = false;
        subscription.UpdatedAt = now;

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Subscription cancelled (auto-renew disabled): user={UserId} sub={SubscriptionId} expires={ExpiresAt}",
            userId, subscription.Id, subscription.ExpiresAt);

        return ApiResponse<CancelSubscriptionResultDto>.Ok(new CancelSubscriptionResultDto(
            subscription.Id,
            AutoRenewDisabled: true,
            subscription.ExpiresAt));
    }
}
