namespace Atlas.Core.Models;

public sealed record ApiResponse<T>(
    bool Success,
    string Code,
    string Message,
    string TraceId,
    T? Data
)
{
    public static ApiResponse<T> Ok(T? data, string traceId) => new(true, ErrorCodes.Success, "OK", traceId, data);
    public static ApiResponse<T> Fail(string code, string message, string traceId) => new(false, code, message, traceId, default);
}