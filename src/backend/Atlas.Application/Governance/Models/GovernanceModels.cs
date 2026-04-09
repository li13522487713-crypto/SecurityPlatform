namespace Atlas.Application.Governance.Models;

public sealed record PackageExportRequest(
    string ManifestId,
    string PackageType);

public sealed record PackageImportRequest(
    string FileName,
    string ContentBase64,
    string ConflictPolicy);

public sealed record PackageAnalyzeRequest(
    string FileName,
    string ContentBase64);

public sealed record PackageOperationResponse(
    string ArtifactId,
    string Status,
    string Message);

public sealed record LicenseOfflineRequest(
    string MachineFingerprint,
    string TenantId,
    string CustomerName);

public sealed record LicenseImportRequest(
    string LicenseContent);

public sealed record LicenseValidateResponse(
    bool IsValid,
    string Edition,
    string? ExpiresAt,
    string Message);

public sealed record ToolAuthorizationPolicyRequest(
    string ToolId,
    string ToolName,
    string PolicyType,
    int RateLimitQuota,
    string? ApprovalFlowId,
    string? ConditionJson,
    bool AuditEnabled);

public sealed record ToolAuthorizationPolicyResponse(
    string Id,
    string ToolId,
    string ToolName,
    string PolicyType,
    int RateLimitQuota,
    bool AuditEnabled);

public sealed record ToolAuthorizationSimulateRequest(
    string ToolId,
    string UserId,
    string? ContextJson);

public sealed record ToolAuthorizationSimulateResponse(
    string Decision,
    string PolicyId,
    int RemainingQuota);

public sealed record DataClassificationRequest(
    string Code,
    string Name,
    int Level,
    string? Scope,
    string? BaselineJson);

public sealed record DataClassificationResponse(
    string Id,
    string Code,
    string Name,
    int Level,
    string Scope,
    string BaselineJson,
    string UpdatedAt);

public sealed record SensitiveLabelRequest(
    string Code,
    string Name,
    string? TargetType,
    string? RuleJson);

public sealed record SensitiveLabelResponse(
    string Id,
    string Code,
    string Name,
    string TargetType,
    string RuleJson,
    string UpdatedAt);

public sealed record DlpPolicyRequest(
    string Name,
    bool Enabled,
    string? ScopeJson,
    string? ChannelRuleJson);

public sealed record DlpPolicyResponse(
    string Id,
    string Name,
    bool Enabled,
    string ScopeJson,
    string ChannelRuleJson,
    string UpdatedAt);

public sealed record OutboundChannelRequest(
    string ChannelKey,
    string DisplayName,
    string ChannelType,
    bool Enabled,
    string? ConfigJson);

public sealed record OutboundChannelResponse(
    string Id,
    string ChannelKey,
    string DisplayName,
    string ChannelType,
    bool Enabled,
    string ConfigJson,
    string UpdatedAt);

public sealed record DlpBindingRequest(
    string AppInstanceId,
    string DataSet,
    IReadOnlyList<string>? MaskFields);

public sealed record DlpTransferJobRequest(
    string AppInstanceId,
    string DataSet,
    string ChannelKey,
    string? Target);

public sealed record ExternalShareApprovalRequest(
    string AppInstanceId,
    string DataSet,
    string Target,
    string Reason);

public sealed record OutboundCheckRequest(
    string AppInstanceId,
    string DataSet,
    string ChannelKey,
    string? Target,
    string? PayloadJson);

public sealed record OutboundCheckResponse(
    string Decision,
    string Reason,
    IReadOnlyList<string> MaskedFields,
    string LeakageEventId,
    string EvidencePackageId);

public sealed record LeakageEventResponse(
    string Id,
    string AppInstanceId,
    string DataSet,
    string ChannelKey,
    string Decision,
    string Reason,
    string TargetSummary,
    string CreatedAt);

public sealed record EvidencePackageResponse(
    string Id,
    string LeakageEventId,
    string SummaryJson,
    string Status,
    string CreatedAt);
