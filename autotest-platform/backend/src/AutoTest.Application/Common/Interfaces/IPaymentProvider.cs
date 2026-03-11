using AutoTest.Domain.Common.Enums;

namespace AutoTest.Application.Common.Interfaces;

public interface IPaymentProviderFactory
{
    IPaymentProviderService GetProvider(PaymentProvider provider);
}

public interface IPaymentProviderService
{
    Task<string> CreatePaymentAsync(Guid subscriptionId, long amountInTiyins, CancellationToken ct = default);
    Task<bool> VerifyPaymentAsync(string providerTransactionId, CancellationToken ct = default);
}
