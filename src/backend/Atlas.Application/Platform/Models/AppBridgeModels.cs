using Atlas.Core.Models;

namespace Atlas.Application.Platform.Models;

public sealed record OnlineAppProjectionItem(
    string AppInstanceId,
    string AppKey,
    string AppName,
    string BridgeMode,
    string RuntimeStatus,
    string HealthStatus,
    string? ReleaseVersion,
    string LastSeenAt);

public sealed record OnlineAppProjectionDetail(
    string AppInstanceId,
    string AppKey,
    string AppName,
    string BridgeMode,
    string RuntimeStatus,
    string HealthStatus,
    string? ReleaseVersion,
    string? BridgeEndpoint,
    IReadOnlyList<string> SupportedCommands,
    string LastSeenAt);

public sealed record AppExposurePolicyResponse(
    string AppInstanceId,
    IReadOnlyList<string> ExposedDataSets,
    IReadOnlyList<string> AllowedCommands,
    IReadOnlyDictionary<string, IReadOnlyList<string>> MaskPolicies,
    string UpdatedAt);

public sealed record AppExposurePolicyUpdateRequest(
    IReadOnlyList<string> ExposedDataSets,
    IReadOnlyList<string> AllowedCommands,
    IReadOnlyDictionary<string, IReadOnlyList<string>> MaskPolicies);

public sealed record AppCommandCreateRequest(
    string AppInstanceId,
    string CommandType,
    string PayloadJson,
    bool DryRun,
    string? Reason = null);

public sealed record AppCommandListItem(
    string CommandId,
    string AppInstanceId,
    string? AppKey,
    string CommandType,
    string RiskLevel,
    bool DryRun,
    string Status,
    string Initiator,
    string CreatedAt,
    string UpdatedAt);

public sealed record AppCommandDetail(
    string CommandId,
    string AppInstanceId,
    string? AppKey,
    string CommandType,
    string RiskLevel,
    string PayloadJson,
    bool DryRun,
    string Status,
    string Initiator,
    string Reason,
    string? ApprovalRequestId,
    string Message,
    string ResultJson,
    string CreatedAt,
    string UpdatedAt,
    string? StartedAt,
    string? CompletedAt);

public sealed record FederatedRegisterRequest(
    string AppInstanceId,
    string AppKey,
    string RuntimeStatus,
    string HealthStatus,
    string? ReleaseVersion,
    string? BridgeEndpoint,
    IReadOnlyList<string> SupportedCommands);

public sealed record FederatedHeartbeatRequest(
    string AppInstanceId,
    string AppKey,
    string RuntimeStatus,
    string HealthStatus,
    string? ReleaseVersion,
    string? BridgeEndpoint,
    IReadOnlyList<string> SupportedCommands);

public sealed record FederatedCommandAckRequest(
    string Message);

public sealed record FederatedCommandResultRequest(
    string Status,
    string ResultJson,
    string Message);

public sealed record ExposedDataQueryRequest(
    string DataSet,
    PagedRequest Paged);

public sealed record ExposedDataQueryResponse(
    string DataSet,
    PagedResult<Dictionary<string, object?>> Result);
