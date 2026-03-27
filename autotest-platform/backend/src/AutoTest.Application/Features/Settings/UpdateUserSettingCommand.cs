using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.Settings;

public record UpdateUserSettingCommand(string Key, string Value) : IRequest<ApiResponse>;

public class UpdateUserSettingCommandValidator : AbstractValidator<UpdateUserSettingCommand>
{
    private static readonly string[] AllowedKeys =
        ["daily_reminder_enabled", "reminder_hour", "push_enabled", "theme", "language"];

    public UpdateUserSettingCommandValidator()
    {
        RuleFor(x => x.Key).Must(k => AllowedKeys.Contains(k))
            .WithMessage($"Key must be one of: {string.Join(", ", AllowedKeys)}");
        RuleFor(x => x.Value).NotEmpty().MaximumLength(500);
    }
}

public class UpdateUserSettingCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider dateTime,
    ICacheService cacheService)
    : IRequestHandler<UpdateUserSettingCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(UpdateUserSettingCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return ApiResponse.Fail("UNAUTHORIZED", "Not authenticated.");

        var userId = currentUser.UserId.Value;
        var now = dateTime.UtcNow;

        var setting = await db.UserSettings
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Key == request.Key, ct);

        if (setting is null)
        {
            setting = new UserSetting
            {
                UserId = userId,
                Key = request.Key,
                Value = request.Value,
                UpdatedAt = now
            };
            db.UserSettings.Add(setting);
        }
        else
        {
            setting.Value = request.Value;
            setting.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);

        // Invalidate cache
        await cacheService.RemoveAsync($"avtolider:settings:{userId}", ct);

        return ApiResponse.Ok();
    }
}
