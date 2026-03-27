using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.Settings;

public record GetUserSettingsQuery : IRequest<ApiResponse<Dictionary<string, string>>>;

public class GetUserSettingsQueryValidator : AbstractValidator<GetUserSettingsQuery>
{
    public GetUserSettingsQueryValidator() { }
}

public class GetUserSettingsQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ICacheService cacheService)
    : IRequestHandler<GetUserSettingsQuery, ApiResponse<Dictionary<string, string>>>
{
    public async Task<ApiResponse<Dictionary<string, string>>> Handle(
        GetUserSettingsQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return ApiResponse<Dictionary<string, string>>.Fail("UNAUTHORIZED", "Not authenticated.");

        var userId = currentUser.UserId.Value;

        var cacheKey = $"avtolider:settings:{userId}";
        var cached = await cacheService.GetAsync<Dictionary<string, string>>(cacheKey, ct);
        if (cached is not null)
            return ApiResponse<Dictionary<string, string>>.Ok(cached);

        var settings = await db.UserSettings
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);

        await cacheService.SetAsync(cacheKey, settings, TimeSpan.FromHours(1), ct);

        return ApiResponse<Dictionary<string, string>>.Ok(settings);
    }
}
