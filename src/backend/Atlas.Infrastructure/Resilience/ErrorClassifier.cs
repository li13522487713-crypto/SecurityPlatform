using System.Net.Http;
using Atlas.Core.Exceptions;
using Atlas.Core.Resilience;
using FluentValidation;
using SqlSugar;

namespace Atlas.Infrastructure.Resilience;

public sealed class ErrorClassifier : IErrorClassifier
{
    public ClassifiedError Classify(Exception exception)
    {
        var ex = Unwrap(exception);

        if (ex is SqlSugarException sx)
        {
            return new ClassifiedError(
                ErrorSeverity.Transient,
                ErrorCategory.Database,
                "SQL_SUGAR",
                sx.Message,
                sx);
        }

        if (ex is HttpRequestException hx)
        {
            return new ClassifiedError(
                ErrorSeverity.Transient,
                ErrorCategory.Network,
                "HTTP_REQUEST",
                hx.Message,
                hx);
        }

        if (ex is TaskCanceledException tcx)
        {
            return new ClassifiedError(
                ErrorSeverity.Transient,
                ErrorCategory.Timeout,
                "TIMEOUT",
                tcx.Message,
                tcx);
        }

        if (ex is TimeoutException tx)
        {
            return new ClassifiedError(
                ErrorSeverity.Transient,
                ErrorCategory.Timeout,
                "TIMEOUT",
                tx.Message,
                tx);
        }

        if (ex is ValidationException vx)
        {
            return new ClassifiedError(
                ErrorSeverity.Permanent,
                ErrorCategory.Validation,
                "VALIDATION",
                vx.Message,
                vx);
        }

        if (ex is BusinessException bx)
        {
            return new ClassifiedError(
                ErrorSeverity.Permanent,
                ErrorCategory.Business,
                bx.Code,
                bx.Message,
                bx);
        }

        if (ex is UnauthorizedAccessException ux)
        {
            return new ClassifiedError(
                ErrorSeverity.Permanent,
                ErrorCategory.Security,
                "UNAUTHORIZED",
                ux.Message,
                ux);
        }

        return new ClassifiedError(
            ErrorSeverity.Unknown,
            ErrorCategory.Unknown,
            "UNKNOWN",
            ex.Message,
            ex);
    }

    private static Exception Unwrap(Exception exception)
    {
        var current = exception;
        while (current is AggregateException agg && agg.InnerExceptions.Count == 1)
            current = agg.InnerExceptions[0];

        return current;
    }
}
