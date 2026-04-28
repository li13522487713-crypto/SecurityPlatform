using Atlas.Connectors.Core.Models;

namespace Atlas.Connectors.Core.Abstractions;

/// <summary>
/// 外部审批 Provider：模板读取 / 提单 / 状态查询 / 三方审批同步。
/// 各 provider 实现负责把统一 DTO 翻译成自己的 apply_data / form 结构。
/// </summary>
public interface IExternalApprovalProvider
{
    string ProviderType { get; }

    /// <summary>读取外部审批模板详情，用于字段映射设计器。</summary>
    Task<ExternalApprovalTemplate> GetTemplateAsync(ConnectorContext context, string externalTemplateId, CancellationToken cancellationToken);

    /// <summary>枚举模板（飞书需要定义码列表，企微部分场景从应用接管）。可选实现，不支持时返回空集合。</summary>
    Task<IReadOnlyList<ExternalApprovalTemplate>> ListTemplatesAsync(ConnectorContext context, CancellationToken cancellationToken);

    Task<ExternalApprovalInstanceRef> SubmitApprovalAsync(ConnectorContext context, ExternalApprovalSubmission submission, CancellationToken cancellationToken);

    Task<ExternalApprovalInstanceRef?> GetInstanceAsync(ConnectorContext context, string externalInstanceId, CancellationToken cancellationToken);

    /// <summary>
    /// 批量按时间窗口拉取近期审批实例 ID（企微 getapprovalinfo / 飞书 approval/v4/instances?start_time...），
    /// 用于"死信事件重放 + 对账校验"。不支持的 provider 直接返回空集合。
    /// </summary>
    Task<ExternalApprovalInstanceIdPage> ListRecentInstanceIdsAsync(
        ConnectorContext context,
        ExternalApprovalInstanceIdQuery query,
        CancellationToken cancellationToken) => Task.FromResult(ExternalApprovalInstanceIdPage.Empty);

    /// <summary>
    /// 三方审批同步（模式 B / C）。本平台流转事件实时同步任务/抄送/状态到外部审批中心。
    /// 不支持的 provider（如企微原生审批）返回 false。
    /// </summary>
    Task<bool> SyncThirdPartyInstanceAsync(ConnectorContext context, ExternalThirdPartyInstancePatch patch, CancellationToken cancellationToken);
}

public sealed record ExternalApprovalInstanceIdQuery
{
    public required DateTimeOffset StartTime { get; init; }

    public required DateTimeOffset EndTime { get; init; }

    public string? TemplateId { get; init; }

    public string? Cursor { get; init; }

    public int Size { get; init; } = 100;
}

public sealed record ExternalApprovalInstanceIdPage
{
    public static readonly ExternalApprovalInstanceIdPage Empty = new()
    {
        InstanceIds = Array.Empty<string>(),
        NextCursor = null,
    };

    public required IReadOnlyList<string> InstanceIds { get; init; }

    public string? NextCursor { get; init; }
}

/// <summary>
/// 三方审批同步增量补丁。
/// </summary>
public sealed record ExternalThirdPartyInstancePatch
{
    public required string ExternalInstanceId { get; init; }

    public required ExternalApprovalStatus NewStatus { get; init; }

    public IReadOnlyList<ExternalThirdPartyTaskPatch>? TaskUpdates { get; init; }

    public string? CommentText { get; init; }

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record ExternalThirdPartyTaskPatch
{
    public required string TaskExternalId { get; init; }

    public required string AssigneeExternalUserId { get; init; }

    public required ExternalApprovalStatus Status { get; init; }
}
