using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.Platform.Entities;

public enum AppBridgeMode
{
    LocalManaged = 0,
    Federated = 1
}

public enum AppBridgeHealthStatus
{
    Unknown = 0,
    Healthy = 1,
    Degraded = 2,
    Offline = 3
}

public enum AppCommandStatus
{
    Pending = 0,
    Dispatched = 1,
    Acked = 2,
    Running = 3,
    Succeeded = 4,
    Failed = 5,
    Cancelled = 6,
    TimedOut = 7
}

public enum AppCommandRiskLevel
{
    Low = 0,
    Medium = 1,
    High = 2
}

[SugarIndex("UX_AppBridgeRegistration_Tenant_AppInstance", nameof(TenantIdValue), OrderByType.Asc, nameof(AppInstanceId), OrderByType.Asc, true)]
public sealed class AppBridgeRegistration : TenantEntity
{
    public AppBridgeRegistration()
        : base(TenantId.Empty)
    {
        AppKey = string.Empty;
        RuntimeStatus = "unknown";
        BridgeEndpoint = string.Empty;
        SupportedCommandsJson = "[]";
        MetadataJson = "{}";
    }

    public AppBridgeRegistration(
        TenantId tenantId,
        long id,
        long appInstanceId,
        string appKey,
        AppBridgeMode mode,
        DateTimeOffset now)
        : base(tenantId)
    {
        Id = id;
        AppInstanceId = appInstanceId;
        AppKey = appKey;
        Mode = mode;
        RuntimeStatus = "unknown";
        HealthStatus = AppBridgeHealthStatus.Unknown;
        BridgeEndpoint = string.Empty;
        SupportedCommandsJson = "[]";
        MetadataJson = "{}";
        LastSeenAt = now;
        UpdatedAt = now;
    }

    public long AppInstanceId { get; private set; }
    public string AppKey { get; private set; }
    public AppBridgeMode Mode { get; private set; }
    public string RuntimeStatus { get; private set; }
    public AppBridgeHealthStatus HealthStatus { get; private set; }
    [SugarColumn(IsNullable = true)]
    public string? ReleaseVersion { get; private set; }
    [SugarColumn(IsNullable = true)]
    public string? BridgeEndpoint { get; private set; }
    public string SupportedCommandsJson { get; private set; }
    public string MetadataJson { get; private set; }
    public DateTimeOffset LastSeenAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void UpdateHeartbeat(
        string runtimeStatus,
        AppBridgeHealthStatus healthStatus,
        string? releaseVersion,
        string? bridgeEndpoint,
        string supportedCommandsJson,
        string metadataJson,
        DateTimeOffset now)
    {
        RuntimeStatus = runtimeStatus;
        HealthStatus = healthStatus;
        ReleaseVersion = releaseVersion;
        BridgeEndpoint = bridgeEndpoint;
        SupportedCommandsJson = supportedCommandsJson;
        MetadataJson = metadataJson;
        LastSeenAt = now;
        UpdatedAt = now;
    }

    public void MarkOffline(DateTimeOffset now)
    {
        RuntimeStatus = "offline";
        HealthStatus = AppBridgeHealthStatus.Offline;
        UpdatedAt = now;
    }
}

[SugarIndex("UX_AppExposurePolicy_Tenant_AppInstance", nameof(TenantIdValue), OrderByType.Asc, nameof(AppInstanceId), OrderByType.Asc, true)]
public sealed class AppExposurePolicy : TenantEntity
{
    public AppExposurePolicy()
        : base(TenantId.Empty)
    {
        ExposedDataSetsJson = "[]";
        AllowedCommandsJson = "[]";
        MaskPoliciesJson = "{}";
    }

    public AppExposurePolicy(
        TenantId tenantId,
        long id,
        long appInstanceId,
        long updatedBy,
        DateTimeOffset updatedAt)
        : base(tenantId)
    {
        Id = id;
        AppInstanceId = appInstanceId;
        ExposedDataSetsJson = "[]";
        AllowedCommandsJson = "[]";
        MaskPoliciesJson = "{}";
        UpdatedBy = updatedBy;
        UpdatedAt = updatedAt;
    }

    public long AppInstanceId { get; private set; }
    public string ExposedDataSetsJson { get; private set; }
    public string AllowedCommandsJson { get; private set; }
    public string MaskPoliciesJson { get; private set; }
    public long UpdatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(string exposedDataSetsJson, string allowedCommandsJson, string maskPoliciesJson, long updatedBy, DateTimeOffset updatedAt)
    {
        ExposedDataSetsJson = exposedDataSetsJson;
        AllowedCommandsJson = allowedCommandsJson;
        MaskPoliciesJson = maskPoliciesJson;
        UpdatedBy = updatedBy;
        UpdatedAt = updatedAt;
    }
}

public sealed class AppCommand : TenantEntity
{
    public AppCommand()
        : base(TenantId.Empty)
    {
        CommandType = string.Empty;
        PayloadJson = "{}";
        ResultJson = "{}";
        IdempotencyKey = string.Empty;
        Initiator = string.Empty;
        Reason = string.Empty;
        RiskLevel = AppCommandRiskLevel.Low;
        Message = string.Empty;
    }

    public AppCommand(
        TenantId tenantId,
        long id,
        long appInstanceId,
        string commandType,
        string payloadJson,
        bool dryRun,
        AppCommandRiskLevel riskLevel,
        string idempotencyKey,
        string initiator,
        string? reason,
        DateTimeOffset createdAt)
        : base(tenantId)
    {
        Id = id;
        AppInstanceId = appInstanceId;
        CommandType = commandType;
        PayloadJson = payloadJson;
        DryRun = dryRun;
        RiskLevel = riskLevel;
        Status = AppCommandStatus.Pending;
        IdempotencyKey = idempotencyKey;
        Initiator = initiator;
        Reason = reason ?? string.Empty;
        Message = string.Empty;
        ResultJson = "{}";
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public long AppInstanceId { get; private set; }
    [SugarColumn(IsNullable = true)]
    public string? AppKey { get; private set; }
    public string CommandType { get; private set; }
    public string PayloadJson { get; private set; }
    public bool DryRun { get; private set; }
    public AppCommandRiskLevel RiskLevel { get; private set; }
    public AppCommandStatus Status { get; private set; }
    public string IdempotencyKey { get; private set; }
    public string Initiator { get; private set; }
    public string Reason { get; private set; }
    [SugarColumn(IsNullable = true)]
    public string? ApprovalRequestId { get; private set; }
    public string Message { get; private set; }
    public string ResultJson { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    [SugarColumn(IsNullable = true)]
    public DateTimeOffset? StartedAt { get; private set; }
    [SugarColumn(IsNullable = true)]
    public DateTimeOffset? CompletedAt { get; private set; }

    public void BindAppKey(string? appKey)
    {
        AppKey = appKey;
    }

    public void MarkDispatched(DateTimeOffset now, string? message = null)
    {
        Status = AppCommandStatus.Dispatched;
        Message = message ?? Message;
        UpdatedAt = now;
    }

    public void BindApprovalRequest(string? approvalRequestId)
    {
        ApprovalRequestId = approvalRequestId;
    }

    public void MarkAcked(DateTimeOffset now, string? message = null)
    {
        Status = AppCommandStatus.Acked;
        Message = message ?? Message;
        UpdatedAt = now;
    }

    public void MarkRunning(DateTimeOffset now, string? message = null)
    {
        Status = AppCommandStatus.Running;
        Message = message ?? Message;
        StartedAt = now;
        UpdatedAt = now;
    }

    public void MarkSucceeded(string resultJson, DateTimeOffset now, string? message = null)
    {
        Status = AppCommandStatus.Succeeded;
        ResultJson = resultJson;
        Message = message ?? Message;
        CompletedAt = now;
        UpdatedAt = now;
    }

    public void MarkFailed(string resultJson, DateTimeOffset now, string? message = null)
    {
        Status = AppCommandStatus.Failed;
        ResultJson = resultJson;
        Message = message ?? Message;
        CompletedAt = now;
        UpdatedAt = now;
    }

    public void MarkCancelled(DateTimeOffset now, string? message = null)
    {
        Status = AppCommandStatus.Cancelled;
        Message = message ?? Message;
        CompletedAt = now;
        UpdatedAt = now;
    }
}
