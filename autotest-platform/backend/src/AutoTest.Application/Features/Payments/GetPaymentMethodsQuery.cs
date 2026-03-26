using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using MediatR;

namespace AutoTest.Application.Features.Payments;

public record PaymentMethodsDto(bool PaymeEnabled, bool ClickEnabled);

public record GetPaymentMethodsQuery : IRequest<ApiResponse<PaymentMethodsDto>>;

public class GetPaymentMethodsQueryHandler(
    ISystemSettingsService settings) : IRequestHandler<GetPaymentMethodsQuery, ApiResponse<PaymentMethodsDto>>
{
    public async Task<ApiResponse<PaymentMethodsDto>> Handle(GetPaymentMethodsQuery request, CancellationToken ct)
    {
        var payme = await settings.GetBoolAsync("payme_enabled", true, ct);
        var click = await settings.GetBoolAsync("click_enabled", true, ct);

        return ApiResponse<PaymentMethodsDto>.Ok(new PaymentMethodsDto(payme, click));
    }
}
