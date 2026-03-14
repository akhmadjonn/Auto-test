using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.Subscriptions;

public record GetPlansQuery(Language Language = Language.UzLatin)
    : IRequest<ApiResponse<List<SubscriptionPlanDto>>>;

public record SubscriptionPlanDto(
    Guid Id,
    LocalizedText Name,
    LocalizedText Description,
    long PriceInTiyins,
    int DurationDays,
    List<LocalizedText> Features);

public class GetPlansQueryHandler(
    IApplicationDbContext db,
    ICacheService cache)
    : IRequestHandler<GetPlansQuery, ApiResponse<List<SubscriptionPlanDto>>>
{
    public async Task<ApiResponse<List<SubscriptionPlanDto>>> Handle(GetPlansQuery request, CancellationToken ct)
    {
        var cacheKey = "avtolider:plans:all";
        var cached = await cache.GetAsync<List<SubscriptionPlanDto>>(cacheKey, ct);
        if (cached is not null)
            return ApiResponse<List<SubscriptionPlanDto>>.Ok(cached);

        var plans = await db.SubscriptionPlans
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.PriceInTiyins)
            .ToListAsync(ct);

        var dtos = plans.Select(p => new SubscriptionPlanDto(
            p.Id,
            p.Name,
            p.Description,
            p.PriceInTiyins,
            p.DurationDays,
            ParseFeatures(p.Features))).ToList();

        await cache.SetAsync(cacheKey, dtos, TimeSpan.FromMinutes(30), ct);

        return ApiResponse<List<SubscriptionPlanDto>>.Ok(dtos);
    }

    private static List<LocalizedText> ParseFeatures(string features)
    {
        if (string.IsNullOrWhiteSpace(features))
            return [];

        // Try parsing as JSON array of LocalizedText objects first
        try
        {
            var parsed = System.Text.Json.JsonSerializer.Deserialize<List<LocalizedText>>(features);
            if (parsed is not null)
                return parsed;
        }
        catch { /* not JSON array of LocalizedText */ }

        // Try parsing as JSON array of plain strings (e.g. ["Feature1","Feature2"])
        try
        {
            var strings = System.Text.Json.JsonSerializer.Deserialize<List<string>>(features);
            if (strings is not null)
                return strings.Select(f => new LocalizedText(f, f, f)).ToList();
        }
        catch { /* not JSON array of strings */ }

        // Fallback: comma-separated plain text
        return features.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(f => new LocalizedText(f, f, f))
            .ToList();
    }
}
