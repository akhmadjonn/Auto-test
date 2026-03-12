using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.Admin;

public record GetSystemSettingsQuery : IRequest<ApiResponse<List<SystemSettingDto>>>;

public record SystemSettingDto(string Key, string Value, string? Description, string? UpdatedBy, DateTimeOffset? UpdatedAt);

public class GetSystemSettingsQueryHandler(
    IApplicationDbContext db) : IRequestHandler<GetSystemSettingsQuery, ApiResponse<List<SystemSettingDto>>>
{
    public async Task<ApiResponse<List<SystemSettingDto>>> Handle(GetSystemSettingsQuery request, CancellationToken ct)
    {
        var settings = await db.SystemSettings
            .AsNoTracking()
            .OrderBy(s => s.Key)
            .Select(s => new SystemSettingDto(s.Key, s.Value, s.Description, s.UpdatedBy, s.UpdatedAt))
            .ToListAsync(ct);

        return ApiResponse<List<SystemSettingDto>>.Ok(settings);
    }
}
