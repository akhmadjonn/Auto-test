using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AutoTest.Application.Features.Payments;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;

namespace AutoTest.Api.Controllers;

[ApiController]
[Route("api/payments")]
[EnableRateLimiting("anonymous")]
public class PaymentsController(ISender mediator, IConfiguration configuration) : ControllerBase
{
    [HttpPost("initiate")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> Initiate([FromBody] InitiatePaymentCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // Payme JSON-RPC webhook — authenticated via Basic auth (merchant_id:secret_key)
    [HttpPost("payme/webhook")]
    public async Task<IActionResult> PaymeWebhook(CancellationToken ct)
    {
        if (!VerifyPaymeAuth())
            return Unauthorized(PaymeError(0, -32504, "Insufficient privilege to perform this method."));

        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(ct);

        JsonDocument doc;
        try { doc = JsonDocument.Parse(body); }
        catch { return BadRequest(PaymeError(0, -32700, "Parse error.")); }

        var root = doc.RootElement;
        var id = root.TryGetProperty("id", out var idEl) ? idEl.GetInt32() : 0;
        var method = root.TryGetProperty("method", out var methodEl) ? methodEl.GetString() ?? "" : "";
        var @params = root.TryGetProperty("params", out var paramsEl) ? paramsEl : default;

        var result = await mediator.Send(new PaymeWebhookCommand(id, method, @params), ct);

        var response = result.Error is null
            ? (object)new { id = result.Id, result = result.Result }
            : new { id = result.Id, error = result.Error };

        return Ok(response);
    }

    // Click Prepare webhook (action=0) and Complete webhook (action=1)
    // Click uses separate URL paths but same handler logic — differentiated by Action field
    [HttpPost("click/prepare")]
    public Task<IActionResult> ClickPrepare([FromForm] ClickWebhookRequest request, CancellationToken ct) =>
        HandleClickWebhook(request, ct);

    [HttpPost("click/complete")]
    public Task<IActionResult> ClickComplete([FromForm] ClickWebhookRequest request, CancellationToken ct) =>
        HandleClickWebhook(request, ct);

    private async Task<IActionResult> HandleClickWebhook(ClickWebhookRequest r, CancellationToken ct)
    {
        var signatureVerified = VerifyClickSignature(r);
        var command = new ClickWebhookCommand(
            r.ClickTransId, r.ServiceId, r.ClickPaydocId, r.MerchantTransId,
            r.MerchantPrepareId, r.Amount, r.Action, r.Error,
            r.ErrorNote, r.SignTime, r.SignString,
            signatureVerified);

        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    private bool VerifyPaymeAuth()
    {
        var authHeader = Request.Headers.Authorization.ToString();
        if (!authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            var credentials = Encoding.UTF8.GetString(
                Convert.FromBase64String(authHeader["Basic ".Length..]));
            var parts = credentials.Split(':', 2);
            if (parts.Length != 2) return false;

            return parts[0] == (configuration["PaymeSettings:MerchantId"] ?? "")
                && parts[1] == (configuration["PaymeSettings:SecretKey"] ?? "");
        }
        catch { return false; }
    }

    private bool VerifyClickSignature(ClickWebhookRequest r)
    {
        var secretKey = configuration["ClickSettings:SecretKey"] ?? "";
        // Prepare: MD5(click_trans_id + service_id + secret_key + merchant_trans_id + amount + action + sign_time)
        // Complete: MD5(click_trans_id + service_id + secret_key + merchant_trans_id + merchant_prepare_id + amount + action + sign_time)
        var raw = r.Action == 0
            ? $"{r.ClickTransId}{r.ServiceId}{secretKey}{r.MerchantTransId}{r.Amount:F2}{r.Action}{r.SignTime}"
            : $"{r.ClickTransId}{r.ServiceId}{secretKey}{r.MerchantTransId}{r.MerchantPrepareId}{r.Amount:F2}{r.Action}{r.SignTime}";

        var expected = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(raw))).ToLower();
        return expected == r.SignString?.ToLower();
    }

    private static object PaymeError(int id, int code, string message) =>
        new { id, error = new { code, message = new { ru = message, uz = message, en = message } } };
}

// Click sends form-encoded POST body with snake_case field names
public class ClickWebhookRequest
{
    [FromForm(Name = "click_trans_id")]
    public long ClickTransId { get; set; }

    [FromForm(Name = "service_id")]
    public long ServiceId { get; set; }

    [FromForm(Name = "click_paydoc_id")]
    public long ClickPaydocId { get; set; }

    [FromForm(Name = "merchant_trans_id")]
    public string MerchantTransId { get; set; } = "";

    [FromForm(Name = "merchant_prepare_id")]
    public string? MerchantPrepareId { get; set; }

    [FromForm(Name = "amount")]
    public decimal Amount { get; set; }

    [FromForm(Name = "action")]
    public int Action { get; set; }

    [FromForm(Name = "error")]
    public int Error { get; set; }

    [FromForm(Name = "error_note")]
    public string? ErrorNote { get; set; }

    [FromForm(Name = "sign_time")]
    public string SignTime { get; set; } = "";

    [FromForm(Name = "sign_string")]
    public string SignString { get; set; } = "";
}
