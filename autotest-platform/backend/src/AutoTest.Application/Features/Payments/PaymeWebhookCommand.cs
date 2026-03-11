using System.Text.Json;
using System.Text.Json.Serialization;
using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Payments;

// Raw JSON-RPC envelope from Payme
public record PaymeWebhookCommand(
    int Id,
    string Method,
    JsonElement Params) : IRequest<PaymeRpcResult>;

// JSON-RPC result envelope (returned directly from controller)
public record PaymeRpcResult(int Id, object? Result, PaymeRpcError? Error);
public record PaymeRpcError(int Code, PaymeErrorMessage Message, string? Data = null);
public record PaymeErrorMessage(string Ru, string Uz, string En);

public class PaymeWebhookCommandHandler(
    IApplicationDbContext db,
    IDateTimeProvider dateTime,
    ILogger<PaymeWebhookCommandHandler> logger)
    : IRequestHandler<PaymeWebhookCommand, PaymeRpcResult>
{
    // Payme error codes
    private const int ErrorInternalSystem = -32400;
    private const int ErrorInsufficientPrivilege = -32504;
    private const int ErrorInvalidAmount = -31001;
    private const int ErrorTransactionNotFound = -31003;
    private const int ErrorTransactionCancelled = -31007;
    private const int ErrorCantDoOperation = -31008;
    private const int ErrorInvalidAccount = -31050;
    private const int ErrorMethodNotFound = -32601;

    public async Task<PaymeRpcResult> Handle(PaymeWebhookCommand request, CancellationToken ct)
    {
        try
        {
            object result = request.Method switch
            {
                "CheckPerformTransaction" => await CheckPerformTransactionAsync(request.Params, ct),
                "CreateTransaction" => await CreateTransactionAsync(request.Params, ct),
                "PerformTransaction" => await PerformTransactionAsync(request.Params, ct),
                "CancelTransaction" => await CancelTransactionAsync(request.Params, ct),
                "CheckTransaction" => await CheckTransactionAsync(request.Params, ct),
                "GetStatement" => await GetStatementAsync(request.Params, ct),
                _ => throw new PaymeRpcException(ErrorMethodNotFound, "Method not found")
            };

            return new PaymeRpcResult(request.Id, result, null);
        }
        catch (PaymeRpcException ex)
        {
            return new PaymeRpcResult(request.Id, null, new PaymeRpcError(
                ex.Code,
                new PaymeErrorMessage(ex.Message, ex.Message, ex.Message),
                ex.Extra));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Payme webhook unhandled error: method={Method}", request.Method);
            return new PaymeRpcResult(request.Id, null, new PaymeRpcError(
                ErrorInternalSystem,
                new PaymeErrorMessage("Internal error", "Internal error", "Internal error"),
                null));
        }
    }

    private async Task<object> CheckPerformTransactionAsync(JsonElement p, CancellationToken ct)
    {
        var amount = p.GetProperty("amount").GetInt64();
        var account = p.GetProperty("account");
        var subscriptionId = ParseSubscriptionId(account);

        var plan = await FindPlanBySubscriptionIdAsync(subscriptionId, amount, ct);
        if (plan is null)
            throw new PaymeRpcException(ErrorInvalidAccount, "Subscription not found or amount mismatch.");

        return new { allow = true };
    }

    private async Task<object> CreateTransactionAsync(JsonElement p, CancellationToken ct)
    {
        var paymeTxnId = p.GetProperty("id").GetString()!;
        var amount = p.GetProperty("amount").GetInt64();
        var account = p.GetProperty("account");
        var subscriptionId = ParseSubscriptionId(account);
        var now = dateTime.UtcNow;
        var createTime = now.ToUnixTimeMilliseconds();

        // Check for existing transaction with this Payme ID
        var existing = await db.PaymentTransactions
            .FirstOrDefaultAsync(t => t.ProviderTransactionId == paymeTxnId, ct);

        if (existing is not null)
        {
            if (existing.Status == PaymentStatus.Failed)
                throw new PaymeRpcException(ErrorTransactionCancelled, "Transaction was cancelled.");

            return new
            {
                create_time = existing.CreatedAt.ToUnixTimeMilliseconds(),
                transaction = existing.Id.ToString(),
                state = existing.Status == PaymentStatus.Completed ? 2 : 1
            };
        }

        // Validate subscription
        var subscription = await db.Subscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.Id == subscriptionId, ct);

        if (subscription is null)
            throw new PaymeRpcException(ErrorInvalidAccount, "Subscription not found.");

        if (subscription.Plan.PriceInTiyins != amount)
            throw new PaymeRpcException(ErrorInvalidAmount, "Amount mismatch.");

        var transaction = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            UserId = subscription.UserId,
            SubscriptionId = subscriptionId,
            Provider = PaymentProvider.Payme,
            ProviderTransactionId = paymeTxnId,
            AmountInTiyins = amount,
            Status = PaymentStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.PaymentTransactions.Add(transaction);
        await db.SaveChangesAsync(ct);

        return new
        {
            create_time = createTime,
            transaction = transaction.Id.ToString(),
            state = 1 // created
        };
    }

    private async Task<object> PerformTransactionAsync(JsonElement p, CancellationToken ct)
    {
        var paymeTxnId = p.GetProperty("id").GetString()!;
        var now = dateTime.UtcNow;

        var transaction = await db.PaymentTransactions
            .FirstOrDefaultAsync(t => t.ProviderTransactionId == paymeTxnId, ct);

        if (transaction is null)
            throw new PaymeRpcException(ErrorTransactionNotFound, "Transaction not found.");

        if (transaction.Status == PaymentStatus.Failed)
            throw new PaymeRpcException(ErrorTransactionCancelled, "Transaction was cancelled.");

        if (transaction.Status == PaymentStatus.Completed)
        {
            return new
            {
                transaction = transaction.Id.ToString(),
                perform_time = transaction.CompletedAt!.Value.ToUnixTimeMilliseconds(),
                state = 2
            };
        }

        // Activate subscription
        if (transaction.SubscriptionId.HasValue)
        {
            var subscription = await db.Subscriptions
                .Include(s => s.Plan)
                .FirstOrDefaultAsync(s => s.Id == transaction.SubscriptionId.Value, ct);

            if (subscription is not null && subscription.Status != SubscriptionStatus.Active)
            {
                subscription.Status = SubscriptionStatus.Active;
                subscription.StartsAt = now;
                subscription.ExpiresAt = now.AddDays(subscription.Plan.DurationDays);
                subscription.UpdatedAt = now;
            }
        }

        transaction.Status = PaymentStatus.Completed;
        transaction.CompletedAt = now;
        transaction.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Payme transaction performed: {TxnId}", paymeTxnId);

        return new
        {
            transaction = transaction.Id.ToString(),
            perform_time = now.ToUnixTimeMilliseconds(),
            state = 2
        };
    }

    private async Task<object> CancelTransactionAsync(JsonElement p, CancellationToken ct)
    {
        var paymeTxnId = p.GetProperty("id").GetString()!;
        var reason = p.TryGetProperty("reason", out var r) ? r.GetInt32() : 1;
        var now = dateTime.UtcNow;

        var transaction = await db.PaymentTransactions
            .FirstOrDefaultAsync(t => t.ProviderTransactionId == paymeTxnId, ct);

        if (transaction is null)
            throw new PaymeRpcException(ErrorTransactionNotFound, "Transaction not found.");

        if (transaction.Status == PaymentStatus.Completed)
            throw new PaymeRpcException(ErrorCantDoOperation, "Cannot cancel completed transaction.");

        transaction.Status = PaymentStatus.Failed;
        transaction.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        return new
        {
            transaction = transaction.Id.ToString(),
            cancel_time = now.ToUnixTimeMilliseconds(),
            state = -1
        };
    }

    private async Task<object> CheckTransactionAsync(JsonElement p, CancellationToken ct)
    {
        var paymeTxnId = p.GetProperty("id").GetString()!;

        var transaction = await db.PaymentTransactions
            .FirstOrDefaultAsync(t => t.ProviderTransactionId == paymeTxnId, ct);

        if (transaction is null)
            throw new PaymeRpcException(ErrorTransactionNotFound, "Transaction not found.");

        var state = transaction.Status switch
        {
            PaymentStatus.Completed => 2,
            PaymentStatus.Failed => -1,
            _ => 1
        };

        return new
        {
            create_time = transaction.CreatedAt.ToUnixTimeMilliseconds(),
            perform_time = transaction.CompletedAt?.ToUnixTimeMilliseconds() ?? 0,
            cancel_time = transaction.Status == PaymentStatus.Failed
                ? transaction.UpdatedAt?.ToUnixTimeMilliseconds() ?? 0 : 0,
            transaction = transaction.Id.ToString(),
            state,
            reason = transaction.Status == PaymentStatus.Failed ? 1 : (int?)null
        };
    }

    private async Task<object> GetStatementAsync(JsonElement p, CancellationToken ct)
    {
        var from = DateTimeOffset.FromUnixTimeMilliseconds(p.GetProperty("from").GetInt64());
        var to = DateTimeOffset.FromUnixTimeMilliseconds(p.GetProperty("to").GetInt64());

        var transactions = await db.PaymentTransactions
            .Where(t => t.Provider == PaymentProvider.Payme
                && t.CreatedAt >= from && t.CreatedAt <= to)
            .ToListAsync(ct);

        var result = transactions.Select(t => new
        {
            id = t.ProviderTransactionId,
            time = t.CreatedAt.ToUnixTimeMilliseconds(),
            amount = t.AmountInTiyins,
            account = new { subscription_id = t.SubscriptionId?.ToString() },
            create_time = t.CreatedAt.ToUnixTimeMilliseconds(),
            perform_time = t.CompletedAt?.ToUnixTimeMilliseconds() ?? 0,
            cancel_time = t.Status == PaymentStatus.Failed ? t.UpdatedAt?.ToUnixTimeMilliseconds() ?? 0 : 0,
            transaction = t.Id.ToString(),
            state = t.Status == PaymentStatus.Completed ? 2 : t.Status == PaymentStatus.Failed ? -1 : 1,
            reason = t.Status == PaymentStatus.Failed ? 1 : (int?)null
        }).ToList();

        return new { transactions = result };
    }

    private static Guid ParseSubscriptionId(JsonElement account)
    {
        if (!account.TryGetProperty("subscription_id", out var prop)
            || !Guid.TryParse(prop.GetString(), out var id))
            throw new PaymeRpcException(ErrorInvalidAccount, "Invalid account: missing subscription_id.");
        return id;
    }

    private async Task<object?> FindPlanBySubscriptionIdAsync(Guid subscriptionId, long amount, CancellationToken ct)
    {
        return await db.Subscriptions
            .Include(s => s.Plan)
            .Where(s => s.Id == subscriptionId && s.Plan.PriceInTiyins == amount)
            .Select(s => new { s.Plan.PriceInTiyins })
            .FirstOrDefaultAsync(ct);
    }
}

public class PaymeRpcException(int code, string message, string? extra = null) : Exception(message)
{
    public int Code { get; } = code;
    public string? Extra { get; } = extra;
}
