namespace Atlas.Core.Models;

public static class ErrorCodes
{
    public const string Success = "SUCCESS";
    public const string ValidationError = "VALIDATION_ERROR";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string ServerError = "SERVER_ERROR";
    public const string AccountLocked = "ACCOUNT_LOCKED";
    public const string PasswordExpired = "PASSWORD_EXPIRED";
    public const string TokenExpired = "TOKEN_EXPIRED";
    public const string IdempotencyRequired = "IDEMPOTENCY_REQUIRED";
    public const string IdempotencyConflict = "IDEMPOTENCY_CONFLICT";
    public const string IdempotencyInProgress = "IDEMPOTENCY_IN_PROGRESS";
    public const string AntiforgeryTokenInvalid = "ANTIFORGERY_TOKEN_INVALID";
    public const string MfaRequired = "MFA_REQUIRED";
}
