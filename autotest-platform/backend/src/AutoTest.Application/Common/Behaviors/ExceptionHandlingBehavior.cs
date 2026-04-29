using AutoTest.Application.Common.Models;
using AutoTest.Domain.Enum;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Common.Behaviors;

// Safety-net behavior for handlers that return BaseResponse / BaseResponse<T>.
// - Converts unhandled exceptions into a structured Error response so no raw 500s escape.
// - Converts FluentValidation failures into a ValidationError with the concrete messages.
// - Leaves all other response types (e.g. legacy ApiResponse<T>) alone by rethrowing,
//   so existing controllers + ExceptionHandlingMiddleware behavior is preserved.
public class ExceptionHandlingBehavior<TRequest, TResponse>(
    ILogger<ExceptionHandlingBehavior<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var responseType = typeof(TResponse);
            var isBase = responseType == typeof(BaseResponse);
            var isGenericBase = responseType.IsGenericType
                && responseType.GetGenericTypeDefinition() == typeof(BaseResponse<>);

            if (!isBase && !isGenericBase)
                throw;

            var (code, message) = MapException(ex);
            logger.LogError(ex, "Unhandled exception in {Request}: {Message}", typeof(TRequest).Name, ex.Message);

            // Both BaseResponse and BaseResponse<T> have public parameterless constructors
            // and inherit BaseResponse, so we can set fields via the base-class cast.
            var instance = Activator.CreateInstance(responseType)!;
            var baseResponse = (BaseResponse)instance;
            baseResponse.ErrorCode = code;
            baseResponse.ErrorMessage = message;
            // HttpStatusCode stays 0 — ResponseService fills it from the catalog.
            return (TResponse)instance;
        }
    }

    private static (int code, string? message) MapException(Exception ex) => ex switch
    {
        ValidationException ve => (
            (int)ErrorEnum.ValidationError,
            string.Join("; ", ve.Errors.Select(e => e.ErrorMessage))),
        UnauthorizedAccessException => ((int)ErrorEnum.Unauthorized, null),
        KeyNotFoundException => ((int)ErrorEnum.ErrorWhileProcess, null),
        _ => ((int)ErrorEnum.ErrorWhileProcess, null),
    };
}
