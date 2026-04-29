using AutoTest.Application.Common.Models;
using AutoTest.Application.Common.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AutoTest.Api.Services;

public class ResponseService(
    LocalizationService localization,
    IHttpContextAccessor httpContextAccessor) : IResponseService
{
    private const string LangHeader = "X-Api-Lang";
    private const string DefaultLang = "uz";

    public IActionResult GetResponse(BaseResponse response)
    {
        if (response.ErrorCode == 0)
        {
            if (response.HttpStatusCode == 0) response.HttpStatusCode = 200;
            return new ObjectResult(response) { StatusCode = response.HttpStatusCode };
        }

        ApplyLocalization(response);
        return new ObjectResult(response) { StatusCode = response.HttpStatusCode };
    }

    public IActionResult GetResponse<T>(BaseResponse<T> response)
    {
        if (response.ErrorCode == 0)
        {
            if (response.HttpStatusCode == 0) response.HttpStatusCode = 200;
            return new ObjectResult(response) { StatusCode = response.HttpStatusCode };
        }

        ApplyLocalization(response);
        return new ObjectResult(response) { StatusCode = response.HttpStatusCode };
    }

    private void ApplyLocalization(BaseResponse response)
    {
        var lang = httpContextAccessor.HttpContext?.Request.Headers[LangHeader].ToString();
        if (string.IsNullOrWhiteSpace(lang)) lang = DefaultLang;

        var entry = localization.GetEntry(response.ErrorCode, lang);

        // Preserve handler-supplied message (e.g., validation details); fill from catalog otherwise.
        if (string.IsNullOrWhiteSpace(response.ErrorMessage))
            response.ErrorMessage = entry.Message;

        if (response.HttpStatusCode == 0)
            response.HttpStatusCode = entry.HttpStatusCode;
    }
}
