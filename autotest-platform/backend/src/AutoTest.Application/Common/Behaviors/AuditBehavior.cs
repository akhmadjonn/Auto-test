using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Common.Behaviors;

// Marker interface for commands that should be audit-logged
public interface IAuditableCommand
{
    AuditAction AuditAction { get; }
    string AuditEntityType { get; }
    string? AuditEntityId { get; }
}

public class AuditBehavior<TRequest, TResponse>(
    IAuditLogService auditLog,
    ICurrentUser currentUser,
    ILogger<AuditBehavior<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IAuditableCommand
    where TResponse : ApiResponse
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next();

        if (!response.Success || currentUser.UserId is null)
            return response;

        try
        {
            await auditLog.LogAsync(
                currentUser.UserId.Value,
                request.AuditAction,
                request.AuditEntityType,
                request.AuditEntityId,
                null, request, null, cancellationToken);
        }
        catch (Exception ex)
        {
            // Audit logging should never fail the main operation
            logger.LogError(ex, "Failed to write audit log for {RequestName}", typeof(TRequest).Name);
        }

        return response;
    }
}
