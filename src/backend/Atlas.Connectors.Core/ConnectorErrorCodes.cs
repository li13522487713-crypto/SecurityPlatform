namespace Atlas.Connectors.Core;

/// <summary>
/// 统一的连接层错误码，避免业务层耦合到具体 provider 的错误号码（如企微 50001 / 60011）。
/// provider 实现负责把外部错误映射到这些常量。
/// </summary>
public static class ConnectorErrorCodes
{
    public const string ProviderNotFound = "CONNECTOR_PROVIDER_NOT_FOUND";
    public const string ProviderDisabled = "CONNECTOR_PROVIDER_DISABLED";
    public const string ProviderConfigInvalid = "CONNECTOR_PROVIDER_CONFIG_INVALID";

    public const string OAuthStateInvalid = "CONNECTOR_OAUTH_STATE_INVALID";
    public const string OAuthStateExpired = "CONNECTOR_OAUTH_STATE_EXPIRED";
    public const string OAuthCodeInvalid = "CONNECTOR_OAUTH_CODE_INVALID";

    public const string TrustedDomainMismatch = "CONNECTOR_TRUSTED_DOMAIN_MISMATCH";
    public const string VisibilityScopeDenied = "CONNECTOR_VISIBILITY_SCOPE_DENIED";

    public const string WebhookSignatureInvalid = "CONNECTOR_WEBHOOK_SIGNATURE_INVALID";
    public const string WebhookReplayDetected = "CONNECTOR_WEBHOOK_REPLAY_DETECTED";
    public const string WebhookDecryptFailed = "CONNECTOR_WEBHOOK_DECRYPT_FAILED";

    public const string TokenAcquireFailed = "CONNECTOR_TOKEN_ACQUIRE_FAILED";
    public const string TokenExpired = "CONNECTOR_TOKEN_EXPIRED";
    public const string TokenInvalid = "CONNECTOR_TOKEN_INVALID";

    public const string IdentityNotFound = "CONNECTOR_IDENTITY_NOT_FOUND";
    public const string IdentityAmbiguous = "CONNECTOR_IDENTITY_AMBIGUOUS";

    public const string DirectorySyncFailed = "CONNECTOR_DIRECTORY_SYNC_FAILED";
    public const string ApprovalSubmitFailed = "CONNECTOR_APPROVAL_SUBMIT_FAILED";
    public const string ApprovalInstanceNotFound = "CONNECTOR_APPROVAL_INSTANCE_NOT_FOUND";
    public const string ApprovalTemplateNotFound = "CONNECTOR_APPROVAL_TEMPLATE_NOT_FOUND";
    public const string ApprovalFieldMappingInvalid = "CONNECTOR_APPROVAL_FIELD_MAPPING_INVALID";
    public const string MessagingFailed = "CONNECTOR_MESSAGING_FAILED";

    public const string CallbackEventProcessFailed = "CONNECTOR_CALLBACK_EVENT_PROCESS_FAILED";

    public const string Unknown = "CONNECTOR_UNKNOWN";
}
