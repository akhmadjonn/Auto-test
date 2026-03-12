using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoTest.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AutoTest.Infrastructure.Services;

// Payme Subscribe API implementation
// Docs: https://developer.help.paycom.uz/ru/subscribe-api
public class PaymePaymentProvider(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<PaymePaymentProvider> logger) : IPaymentProviderService
{
    private readonly string _merchantId = configuration["PaymeSettings:MerchantId"] ?? "";
    private readonly string _secretKey = configuration["PaymeSettings:SecretKey"] ?? "";
    private readonly string _baseUrl = configuration["PaymeSettings:BaseUrl"]
        ?? "https://checkout.paycom.uz/api";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<string> CreatePaymentAsync(Guid subscriptionId, long amountInTiyins, CancellationToken ct = default)
    {
        // For Payme: create a receipt and return its ID as the "transaction"
        var receiptResult = await CreateReceiptAsync(subscriptionId, amountInTiyins, ct);
        return receiptResult;
    }

    public async Task<bool> VerifyPaymentAsync(string providerTransactionId, CancellationToken ct = default)
    {
        var request = new PaymeRpcRequest("receipts.get", new { id = providerTransactionId });
        var response = await SendRequestAsync<PaymeReceiptResponse>(request, ct);
        return response?.Receipt?.State == 4; // 4 = paid
    }

    public async Task<PaymentChargeResult> ChargeAsync(
        string cardToken, long amountInTiyins, Guid subscriptionId, string description,
        CancellationToken ct = default)
    {
        try
        {
            // Step 1: Create receipt
            var receiptId = await CreateReceiptAsync(subscriptionId, amountInTiyins, ct);

            // Step 2: Pay receipt with card token
            var payRequest = new PaymeRpcRequest("receipts.pay", new
            {
                id = receiptId,
                token = cardToken
            });

            var payResponse = await SendRequestAsync<PaymeReceiptResponse>(payRequest, ct);

            if (payResponse?.Receipt?.State == 4) // paid
                return new PaymentChargeResult(true, receiptId, null, null);

            return new PaymentChargeResult(false, null, "PAYMENT_FAILED", "Payment was not confirmed by Payme.");
        }
        catch (PaymeException ex)
        {
            logger.LogError(ex, "Payme charge failed: code={Code} message={Message}", ex.Code, ex.Message);
            return new PaymentChargeResult(false, null, $"PAYME_{ex.Code}", ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Payme charge unexpected error");
            return new PaymentChargeResult(false, null, "INTERNAL_ERROR", "Payment provider error.");
        }
    }

    private async Task<string> CreateReceiptAsync(Guid subscriptionId, long amountInTiyins, CancellationToken ct)
    {
        var request = new PaymeRpcRequest("receipts.create", new
        {
            amount = amountInTiyins,
            account = new { subscription_id = subscriptionId.ToString() },
            detail = new
            {
                receipt_type = 0,
                shipping = new { title = "Avtolider subscription", price = amountInTiyins }
            }
        });

        var response = await SendRequestAsync<PaymeReceiptCreateResponse>(request, ct);
        if (response?.Receipt?.Id is null)
            throw new PaymeException(-31050, "Failed to create receipt.");

        return response.Receipt.Id;
    }

    private async Task<T?> SendRequestAsync<T>(PaymeRpcRequest request, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("Payme");
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_merchantId}:{_secretKey}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        var json = JsonSerializer.Serialize(request, SerializerOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var httpResponse = await client.PostAsync(_baseUrl, content, ct);

        var responseJson = await httpResponse.Content.ReadAsStringAsync(ct);
        var rpcResponse = JsonSerializer.Deserialize<PaymeRpcResponse<T>>(responseJson, SerializerOptions);

        if (rpcResponse?.Error is not null)
            throw new PaymeException(rpcResponse.Error.Code, rpcResponse.Error.Message.Ru ?? "Payme error");

        return rpcResponse!.Result;
    }

    // DTOs
    private record PaymeRpcRequest(
        [property: JsonPropertyName("method")] string Method,
        [property: JsonPropertyName("params")] object Params,
        [property: JsonPropertyName("id")] int Id = 1);

    private record PaymeRpcResponse<T>(
        [property: JsonPropertyName("result")] T? Result,
        [property: JsonPropertyName("error")] PaymeError? Error);

    private record PaymeError(
        [property: JsonPropertyName("code")] int Code,
        [property: JsonPropertyName("message")] PaymeErrorMessage Message);

    private record PaymeErrorMessage(
        [property: JsonPropertyName("ru")] string? Ru,
        [property: JsonPropertyName("uz")] string? Uz);

    private record PaymeReceiptCreateResponse(
        [property: JsonPropertyName("receipt")] PaymeReceiptDto? Receipt);

    private record PaymeReceiptResponse(
        [property: JsonPropertyName("receipt")] PaymeReceiptStateDto? Receipt);

    private record PaymeReceiptDto(
        [property: JsonPropertyName("_id")] string? Id,
        [property: JsonPropertyName("state")] int State);

    private record PaymeReceiptStateDto(
        [property: JsonPropertyName("_id")] string? Id,
        [property: JsonPropertyName("state")] int State);
}

public class PaymeException(int code, string message) : Exception(message)
{
    public int Code { get; } = code;
}
