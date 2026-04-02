namespace Atlas.Core.Resilience;

public enum ErrorSeverity
{
    Transient,
    Permanent,
    Unknown
}

public enum ErrorCategory
{
    Network,
    Timeout,
    Database,
    Validation,
    Business,
    Infrastructure,
    Security,
    Unknown
}

public sealed record ClassifiedError(
    ErrorSeverity Severity,
    ErrorCategory Category,
    string Code,
    string Message,
    Exception? Inner = null);
