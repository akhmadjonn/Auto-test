using AutoTest.Application.Common.Interfaces;
using AutoTest.Domain.Common.Enums;

namespace AutoTest.Infrastructure.Services;

public class PaymentProviderFactory(
    PaymePaymentProvider payme,
    ClickPaymentProvider click) : IPaymentProviderFactory
{
    public IPaymentProviderService GetProvider(PaymentProvider provider) => provider switch
    {
        PaymentProvider.Payme => payme,
        PaymentProvider.Click => click,
        _ => throw new ArgumentOutOfRangeException(nameof(provider), $"Unknown payment provider: {provider}")
    };
}
