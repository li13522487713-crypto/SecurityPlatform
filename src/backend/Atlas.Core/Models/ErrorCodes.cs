namespace Atlas.Core.Models;

public static class ErrorCodes
{
    public const string Success = "SUCCESS";
    public const string ValidationError = "VALIDATION_ERROR";
    public const string Conflict = "CONFLICT";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string ServerError = "SERVER_ERROR";
    public const string AccountLocked = "ACCOUNT_LOCKED";
    public const string PasswordExpired = "PASSWORD_EXPIRED";
    public const string TokenExpired = "TOKEN_EXPIRED";
    public const string ProjectRequired = "PROJECT_REQUIRED";
    public const string ProjectNotFound = "PROJECT_NOT_FOUND";
    public const string ProjectDisabled = "PROJECT_DISABLED";
    public const string CrossTenantForbidden = "CROSS_TENANT_FORBIDDEN";
    public const string ProjectForbidden = "PROJECT_FORBIDDEN";
    public const string AppContextRequired = "APP_CONTEXT_REQUIRED";
    public const string AppMigrationPending = "APP_MIGRATION_PENDING";
    public const string IdempotencyRequired = "IDEMPOTENCY_REQUIRED";
    public const string IdempotencyConflict = "IDEMPOTENCY_CONFLICT";
    public const string IdempotencyInProgress = "IDEMPOTENCY_IN_PROGRESS";
    public const string DatabaseCorrupted = "DATABASE_CORRUPTED";
    public const string AntiforgeryTokenInvalid = "ANTIFORGERY_TOKEN_INVALID";
    public const string MfaRequired = "MFA_REQUIRED";
    public const string LicenseExpired = "LICENSE_EXPIRED";
    public const string LicenseInvalid = "LICENSE_INVALID";
    public const string LicenseLimitExceeded = "LICENSE_LIMIT_EXCEEDED";
}
