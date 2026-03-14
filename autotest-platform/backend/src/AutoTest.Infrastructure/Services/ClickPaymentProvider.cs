using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoTest.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AutoTest.Infrastructure.Services;

// Click Merchant API — Card Token (Subscribe) implementation
// Docs: https://docs.click.uz/
public class ClickPaymentProvider(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<ClickPaymentProvider> logger) : IPaymentProviderService
{
    private readonly string _serviceId = configuration["ClickSettings:ServiceId"] ?? "";
    private readonly string _merchantId = configuration["ClickSettings:MerchantId"] ?? "";
    private readonly string _merchantUserId = configuration["ClickSettings:MerchantUserId"] ?? "";
    private readonly string _secretKey = configuration["ClickSettings:SecretKey"] ?? "";
    private readonly string _baseUrl = configuration["ClickSettings:BaseUrl"]
        ?? "https://api.click.uz/v2/merchant";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<string> CreatePaymentAsync(Guid subscriptionId, long amountInTiyins, CancellationToken ct = default)
    {
        var amountUzs = amountInTiyins / 100.0m; // tiyins → sum
        var result = await InitiatePaymentAsync(subscriptionId.ToString(), amountUzs, ct);
        return result;
    }

    public string GenerateCheckoutUrl(string providerTransactionId, long amountInTiyins, Guid subscriptionId)
    {
        var amountUzs = amountInTiyins / 100.0m;
        var checkoutBase = configuration["ClickSettings:CheckoutUrl"] ?? "https://my.click.uz/services/pay";
        return $"{checkoutBase}?service_id={_serviceId}&merchant_id={_merchantId}&amount={amountUzs}&transaction_param={subscriptionId}";
    }

    public async Task<bool> VerifyPaymentAsync(string providerTransactionId, CancellationToken ct = default)
    {
        var client = CreateAuthenticatedClient();
        var url = $"{_baseUrl}/payment/status/{_serviceId}/{providerTransactionId}";
        using var response = await client.GetAsync(url, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<ClickStatusResponse>(json, JsonOptions);
        return result?.PaymentStatus == 2; // 2 = confirmed
    }

    public async Task<PaymentChargeResult> ChargeAsync(
        string cardToken, long amountInTiyins, Guid subscriptionId, string description,
        CancellationToken ct = default)
    {
        try
        {
            var amountUzs = amountInTiyins / 100.0m;
            var client = CreateAuthenticatedClient();

            var payload = new
            {
                service_id = _serviceId,
                card_token = cardToken,
                amount = amountUzs,
                merchant_trans_id = subscriptionId.ToString(),
                merchant_prepare_id = subscriptionId.ToString(),
                param2 = description
            };

            var json = JsonSerializer.Serialize(payload, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await client.PostAsync($"{_baseUrl}/payment/pay_with_token", content, ct);

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<ClickPaymentResponse>(responseJson, JsonOptions);

            if (result is null || result.Error != 0)
            {
                var errMsg = result?.ErrorNote ?? "Click payment failed";
                logger.LogWarning("Click charge failed: error={Error} note={Note}",
                    result?.Error, result?.ErrorNote);
                return new PaymentChargeResult(false, null, $"CLICK_{result?.Error}", errMsg);
            }

            return new PaymentChargeResult(true, result.PaymentId?.ToString(), null, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Click charge unexpected error");
            return new PaymentChargeResult(false, null, "INTERNAL_ERROR", "Payment provider error.");
        }
    }

    private async Task<string> InitiatePaymentAsync(string merchantTransId, decimal amountUzs, CancellationToken ct)
    {
        var client = CreateAuthenticatedClient();
        var payload = new
        {
            service_id = _serviceId,
            amount = amountUzs,
            merchant_trans_id = merchantTransId
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync($"{_baseUrl}/invoice/create", content, ct);
        var responseJson = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<ClickInvoiceResponse>(responseJson, JsonOptions);

        if (result is null || result.Error != 0)
            throw new InvalidOperationException($"Click invoice creation failed: {result?.ErrorNote}");

        return result.InvoiceId?.ToString() ?? merchantTransId;
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var digest = ComputeDigest(timestamp);
        var client = httpClientFactory.CreateClient("Click");
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("Auth", $"{_merchantId}:{_merchantUserId}:{digest}:{timestamp}");
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        return client;
    }

    private string ComputeDigest(long timestamp) =>
        Convert.ToHexString(
            MD5.HashData(Encoding.UTF8.GetBytes($"{timestamp}{_secretKey}")))
        .ToLower();

    // DTOs
    private record ClickStatusResponse(
        [property: JsonPropertyName("payment_status")] int PaymentStatus);

    private record ClickPaymentResponse(
        [property: JsonPropertyName("error")] int Error,
        [property: JsonPropertyName("error_note")] string? ErrorNote,
        [property: JsonPropertyName("payment_id")] long? PaymentId);

    private record ClickInvoiceResponse(
        [property: JsonPropertyName("error")] int Error,
        [property: JsonPropertyName("error_note")] string? ErrorNote,
        [property: JsonPropertyName("invoice_id")] long? InvoiceId);
}
