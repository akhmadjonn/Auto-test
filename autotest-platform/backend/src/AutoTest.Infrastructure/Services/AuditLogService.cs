using System.Text.Json;
using AutoTest.Application.Common.Interfaces;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AutoTest.Infrastructure.Services;

public class AuditLogService(
    IApplicationDbContext db,
    IDateTimeProvider dateTime,
    ILogger<AuditLogService> logger) : IAuditLogService
{
    public async Task LogAsync(
        Guid userId, AuditAction action, string entityType, string? entityId,
        object? oldValues, object? newValues, string? ipAddress, CancellationToken ct = default)
    {
        var log = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValues = oldValues is not null ? JsonSerializer.Serialize(oldValues) : null,
            NewValues = newValues is not null ? JsonSerializer.Serialize(newValues) : null,
            IpAddress = ipAddress,
            CreatedAt = dateTime.UtcNow
        };

        db.AuditLogs.Add(log);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Audit: {Action} on {EntityType}/{EntityId} by {UserId}",
            action, entityType, entityId, userId);
    }
}
