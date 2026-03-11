using AutoTest.Domain.Common.Enums;

namespace AutoTest.Application.Common.Interfaces;

public interface IAuditLogService
{
    Task LogAsync(Guid userId, AuditAction action, string entityType, string? entityId, object? oldValues, object? newValues, string? ipAddress, CancellationToken ct = default);
}
