namespace Atlas.Application.Microflows.Contracts;

public static class MicroflowApiErrorCode
{
    public const string MicroflowNotFound = "MICROFLOW_NOT_FOUND";
    public const string MicroflowNameDuplicated = "MICROFLOW_NAME_DUPLICATED";
    public const string MicroflowSchemaInvalid = "MICROFLOW_SCHEMA_INVALID";
    public const string MicroflowValidationFailed = "MICROFLOW_VALIDATION_FAILED";
    public const string MicroflowVersionConflict = "MICROFLOW_VERSION_CONFLICT";
    public const string MicroflowPublishBlocked = "MICROFLOW_PUBLISH_BLOCKED";
    public const string MicroflowReferenceBlocked = "MICROFLOW_REFERENCE_BLOCKED";
    public const string MicroflowPermissionDenied = "MICROFLOW_PERMISSION_DENIED";
    public const string MicroflowWorkspaceForbidden = "MICROFLOW_WORKSPACE_FORBIDDEN";
    public const string MicroflowUnauthorized = "MICROFLOW_UNAUTHORIZED";
    public const string MicroflowArchived = "MICROFLOW_ARCHIVED";
    public const string MicroflowFolderNotFound = "MICROFLOW_FOLDER_NOT_FOUND";
    public const string MicroflowFolderNameDuplicated = "MICROFLOW_FOLDER_NAME_DUPLICATED";
    public const string MicroflowFolderNotEmpty = "MICROFLOW_FOLDER_NOT_EMPTY";
    public const string MicroflowFolderDepthExceeded = "MICROFLOW_FOLDER_DEPTH_EXCEEDED";
    public const string MicroflowFolderCycle = "MICROFLOW_FOLDER_CYCLE";
    public const string MicroflowRunFailed = "MICROFLOW_RUN_FAILED";
    public const string MicroflowRunCancelled = "MICROFLOW_RUN_CANCELLED";
    public const string MicroflowMetadataNotFound = "MICROFLOW_METADATA_NOT_FOUND";
    public const string MicroflowMetadataLoadFailed = "MICROFLOW_METADATA_LOAD_FAILED";
    public const string MicroflowStorageError = "MICROFLOW_STORAGE_ERROR";
    public const string MicroflowNetworkError = "MICROFLOW_NETWORK_ERROR";
    public const string MicroflowTimeout = "MICROFLOW_TIMEOUT";
    public const string MicroflowServiceUnavailable = "MICROFLOW_SERVICE_UNAVAILABLE";
    public const string MicroflowUnknownError = "MICROFLOW_UNKNOWN_ERROR";
}
