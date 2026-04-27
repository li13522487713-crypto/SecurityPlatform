using Atlas.Application.Microflows.Contracts;

namespace Atlas.Application.Microflows.Exceptions;

public static class MicroflowExceptionMapper
{
    public static MicroflowMappedException ToApiError(Exception exception, string traceId)
    {
        if (exception is MicroflowApiException microflowException)
        {
            return new MicroflowMappedException(
                microflowException.HttpStatus,
                new MicroflowApiError
                {
                    Code = microflowException.Code,
                    Message = microflowException.Message,
                    Details = microflowException.Details,
                    FieldErrors = microflowException.FieldErrors,
                    ValidationIssues = microflowException.ValidationIssues,
                    Retryable = microflowException.Retryable,
                    HttpStatus = microflowException.HttpStatus,
                    TraceId = traceId
                });
        }

        if (exception is UnauthorizedAccessException)
        {
            return Create(403, MicroflowApiErrorCode.MicroflowPermissionDenied, "没有访问该微流资源的权限。", traceId);
        }

        if (exception is KeyNotFoundException)
        {
            return Create(404, MicroflowApiErrorCode.MicroflowNotFound, "微流资源不存在。", traceId);
        }

        if (IsValidationException(exception))
        {
            return Create(422, MicroflowApiErrorCode.MicroflowValidationFailed, exception.Message, traceId);
        }

        if (exception is OperationCanceledException)
        {
            return Create(400, MicroflowApiErrorCode.MicroflowRunCancelled, "微流请求已取消。", traceId);
        }

        return Create(500, MicroflowApiErrorCode.MicroflowUnknownError, "微流服务发生未知错误。", traceId);
    }

    private static MicroflowMappedException Create(int httpStatus, string code, string message, string traceId, bool retryable = false)
    {
        return new MicroflowMappedException(
            httpStatus,
            new MicroflowApiError
            {
                Code = code,
                Message = message,
                Retryable = retryable,
                HttpStatus = httpStatus,
                TraceId = traceId
            });
    }

    private static bool IsValidationException(Exception exception)
    {
        var type = exception.GetType();
        return string.Equals(type.Name, "ValidationException", StringComparison.Ordinal)
            || string.Equals(type.FullName, "FluentValidation.ValidationException", StringComparison.Ordinal);
    }
}

public sealed record MicroflowMappedException(int HttpStatus, MicroflowApiError Error);
