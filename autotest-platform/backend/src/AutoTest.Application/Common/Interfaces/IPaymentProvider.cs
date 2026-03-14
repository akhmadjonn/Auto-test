using AutoTest.Domain.Common.Enums;

namespace AutoTest.Application.Common.Interfaces;

public record PaymentChargeResult(bool Success, string? TransactionId, string? ErrorCode, string? ErrorMessage);

public interface IPaymentProviderFactory
{
    IPaymentProviderService GetProvider(PaymentProvider provider);
}

public interface IPaymentProviderService
{
    // Initiate payment flow — returns provider transaction ID
    Task<string> CreatePaymentAsync(Guid subscriptionId, long amountInTiyins, CancellationToken ct = default);

    // Generate checkout URL for user redirect after CreatePaymentAsync
    string GenerateCheckoutUrl(string providerTransactionId, long amountInTiyins, Guid subscriptionId);

    // Verify payment status by provider transaction ID
    Task<bool> VerifyPaymentAsync(string providerTransactionId, CancellationToken ct = default);

    // Charge a stored card token directly (used by recurring billing)
    Task<PaymentChargeResult> ChargeAsync(
        string cardToken, long amountInTiyins, Guid subscriptionId, string description,
        CancellationToken ct = default);
}
