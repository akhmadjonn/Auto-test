using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Admin;

public record UpdateSystemSettingCommand(string Key, string Value) : IRequest<ApiResponse>;

public class UpdateSystemSettingCommandValidator : AbstractValidator<UpdateSystemSettingCommand>
{
    public UpdateSystemSettingCommandValidator()
    {
        RuleFor(x => x.Key).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Value).NotNull().MaximumLength(1000);
    }
}

public class UpdateSystemSettingCommandHandler(
    IApplicationDbContext db,
    ISystemSettingsService settingsService,
    ICurrentUser currentUser,
    IAuditLogService auditLog,
    IDateTimeProvider dateTime,
    ILogger<UpdateSystemSettingCommandHandler> logger) : IRequestHandler<UpdateSystemSettingCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(UpdateSystemSettingCommand request, CancellationToken ct)
    {
        var setting = await db.SystemSettings.FindAsync([request.Key], ct);
        if (setting is null)
            return ApiResponse.Fail("SETTING_NOT_FOUND", $"Setting '{request.Key}' not found.");

        var oldValue = setting.Value;
        setting.Value = request.Value;
        setting.UpdatedBy = currentUser.UserId?.ToString();
        setting.UpdatedAt = dateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        // Push to Redis + Pub/Sub invalidation
        await settingsService.SetAsync(request.Key, request.Value, ct);

        if (currentUser.UserId.HasValue)
            await auditLog.LogAsync(
                currentUser.UserId.Value,
                Domain.Common.Enums.AuditAction.Update,
                "SystemSetting", request.Key,
                new { Key = request.Key, Value = oldValue },
                new { Key = request.Key, Value = request.Value },
                null, ct);

        logger.LogInformation("SystemSetting '{Key}' updated: '{Old}' → '{New}'", request.Key, oldValue, request.Value);
        return ApiResponse.Ok();
    }
}
