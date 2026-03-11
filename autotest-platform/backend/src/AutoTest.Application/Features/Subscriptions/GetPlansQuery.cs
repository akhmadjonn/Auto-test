using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.Subscriptions;

public record GetPlansQuery(Language Language = Language.UzLatin)
    : IRequest<ApiResponse<List<SubscriptionPlanDto>>>;

public record SubscriptionPlanDto(
    Guid Id,
    string Name,
    string Description,
    long PriceInTiyins,
    int DurationDays,
    string Features,
    bool IsActive);

public class GetPlansQueryHandler(
    IApplicationDbContext db,
    ICacheService cache)
    : IRequestHandler<GetPlansQuery, ApiResponse<List<SubscriptionPlanDto>>>
{
    public async Task<ApiResponse<List<SubscriptionPlanDto>>> Handle(GetPlansQuery request, CancellationToken ct)
    {
        var cacheKey = $"avtolider:plans:{request.Language}";
        var cached = await cache.GetAsync<List<SubscriptionPlanDto>>(cacheKey, ct);
        if (cached is not null)
            return ApiResponse<List<SubscriptionPlanDto>>.Ok(cached);

        var plans = await db.SubscriptionPlans
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.PriceInTiyins)
            .Select(p => new SubscriptionPlanDto(
                p.Id,
                p.Name.Get(request.Language),
                p.Description.Get(request.Language),
                p.PriceInTiyins,
                p.DurationDays,
                p.Features,
                p.IsActive))
            .ToListAsync(ct);

        await cache.SetAsync(cacheKey, plans, TimeSpan.FromMinutes(30), ct);

        return ApiResponse<List<SubscriptionPlanDto>>.Ok(plans);
    }
}
