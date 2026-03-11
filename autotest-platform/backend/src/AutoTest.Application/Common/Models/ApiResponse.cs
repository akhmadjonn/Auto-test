using System.Text.Json.Serialization;

namespace AutoTest.Application.Common.Models;

public class ApiResponse
{
    public bool Success { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ApiError? Error { get; init; }

    public static ApiResponse Ok() => new() { Success = true };
    public static ApiResponse Fail(string code, string message) => new() { Success = false, Error = new ApiError(code, message) };
}

public class ApiResponse<T> : ApiResponse
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public T? Data { get; init; }

    public static ApiResponse<T> Ok(T data) => new() { Success = true, Data = data };
    public new static ApiResponse<T> Fail(string code, string message) => new() { Success = false, Error = new ApiError(code, message) };
}

public record ApiError(string Code, string Message);
