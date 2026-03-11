using AutoTest.Application.Common.Interfaces;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Payments;

// Click Merchant API: Prepare (action=0) + Complete (action=1) webhook
// Signature is verified by the controller before dispatching this command
public record ClickWebhookCommand(
    long ClickTransId,
    long ServiceId,
    long ClickPaydocId,
    string MerchantTransId,       // Our subscription ID
    string? MerchantPrepareId,    // Our internal transaction ID (for Complete step)
    decimal Amount,
    int Action,                   // 0=Prepare, 1=Complete
    int Error,
    string? ErrorNote,
    string SignTime,
    string SignString,
    bool SignatureVerified) : IRequest<ClickWebhookResult>;

public record ClickWebhookResult(
    int Error,
    string ErrorNote,
    long ClickTransId,
    string? MerchantTransId,
    string? MerchantPrepareId,
    string? MerchantConfirmId);

public class ClickWebhookCommandHandler(
    IApplicationDbContext db,
    IDateTimeProvider dateTime,
    ILogger<ClickWebhookCommandHandler> logger)
    : IRequestHandler<ClickWebhookCommand, ClickWebhookResult>
{
    // Click error codes
    private const int Ok = 0;
    private const int SignFailed = -1;
    private const int InvalidAmount = -2;
    private const int ActionNotFound = -3;
    private const int AlreadyPaid = -4;
    private const int UserNotFound = -5;
    private const int TransactionNotFound = -6;
    private const int TransactionCancelled = -9;

    public async Task<ClickWebhookResult> Handle(ClickWebhookCommand request, CancellationToken ct)
    {
        if (!request.SignatureVerified)
        {
            logger.LogWarning("Click signature verification failed: trans_id={TransId}", request.ClickTransId);
            return Err(request, SignFailed, "Invalid sign");
        }

        if (request.Action == 0)
            return await HandlePrepareAsync(request, ct);

        if (request.Action == 1)
            return await HandleCompleteAsync(request, ct);

        return Err(request, ActionNotFound, "Action not found");
    }

    private async Task<ClickWebhookResult> HandlePrepareAsync(ClickWebhookCommand request, CancellationToken ct)
    {
        if (!Guid.TryParse(request.MerchantTransId, out var subscriptionId))
            return Err(request, UserNotFound, "Invalid subscription ID");

        var subscription = await db.Subscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.Id == subscriptionId, ct);

        if (subscription is null)
            return Err(request, UserNotFound, "Subscription not found");

        // Click sends amount in UZS (sum), our DB stores tiyins — 1 sum = 100 tiyins
        var expectedAmount = subscription.Plan.PriceInTiyins / 100.0m;
        if (Math.Abs(request.Amount - expectedAmount) > 0.01m)
            return Err(request, InvalidAmount, "Incorrect payment amount");

        var now = dateTime.UtcNow;
        var transaction = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            UserId = subscription.UserId,
            SubscriptionId = subscriptionId,
            Provider = PaymentProvider.Click,
            ProviderTransactionId = request.ClickTransId.ToString(),
            AmountInTiyins = subscription.Plan.PriceInTiyins,
            Status = PaymentStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.PaymentTransactions.Add(transaction);
        await db.SaveChangesAsync(ct);

        return new ClickWebhookResult(Ok, "Success", request.ClickTransId,
            request.MerchantTransId, transaction.Id.ToString(), null);
    }

    private async Task<ClickWebhookResult> HandleCompleteAsync(ClickWebhookCommand request, CancellationToken ct)
    {
        if (request.Error != 0)
        {
            // Click reports payment failure — mark pending transaction as failed
            if (request.MerchantPrepareId is not null
                && Guid.TryParse(request.MerchantPrepareId, out var failedTxnId))
            {
                var failedTxn = await db.PaymentTransactions
                    .FirstOrDefaultAsync(t => t.Id == failedTxnId, ct);
                if (failedTxn is not null)
                {
                    failedTxn.Status = PaymentStatus.Failed;
                    failedTxn.UpdatedAt = dateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                }
            }
            return Err(request, request.Error, request.ErrorNote ?? "Payment failed");
        }

        if (!Guid.TryParse(request.MerchantPrepareId, out var txnId))
            return Err(request, TransactionNotFound, "Invalid merchant prepare ID");

        var transaction = await db.PaymentTransactions
            .FirstOrDefaultAsync(t => t.Id == txnId, ct);

        if (transaction is null)
            return Err(request, TransactionNotFound, "Transaction not found");

        if (transaction.Status == PaymentStatus.Completed)
            return Err(request, AlreadyPaid, "Already paid");

        if (transaction.Status == PaymentStatus.Failed)
            return Err(request, TransactionCancelled, "Transaction cancelled");

        var now = dateTime.UtcNow;
        transaction.Status = PaymentStatus.Completed;
        transaction.CompletedAt = now;
        transaction.UpdatedAt = now;

        // Activate subscription upon confirmed payment
        if (transaction.SubscriptionId.HasValue)
        {
            var subscription = await db.Subscriptions
                .Include(s => s.Plan)
                .FirstOrDefaultAsync(s => s.Id == transaction.SubscriptionId.Value, ct);

            if (subscription is not null)
            {
                subscription.Status = SubscriptionStatus.Active;
                subscription.StartsAt = now;
                subscription.ExpiresAt = now.AddDays(subscription.Plan.DurationDays);
                subscription.UpdatedAt = now;
            }
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation("Click payment confirmed: click_trans={ClickTrans} txn={TxnId}",
            request.ClickTransId, txnId);

        return new ClickWebhookResult(Ok, "Success", request.ClickTransId,
            request.MerchantTransId, request.MerchantPrepareId, transaction.Id.ToString());
    }

    private static ClickWebhookResult Err(ClickWebhookCommand r, int code, string note) =>
        new(code, note, r.ClickTransId, r.MerchantTransId, r.MerchantPrepareId, null);
}
