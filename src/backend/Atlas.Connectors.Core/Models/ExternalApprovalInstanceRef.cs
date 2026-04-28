namespace Atlas.Connectors.Core.Models;

/// <summary>
/// 外部审批实例的引用（提单成功后由 provider 返回，本地落库到 ExternalApprovalInstanceLink）。
/// </summary>
public sealed record ExternalApprovalInstanceRef
{
    public required string ProviderType { get; init; }

    public required string ProviderTenantId { get; init; }

    public required string ExternalInstanceId { get; init; }

    public string? ExternalTemplateId { get; init; }

    public required ExternalApprovalStatus Status { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public string? Url { get; init; }

    public string? RawJson { get; init; }
}

/// <summary>
/// 跨 provider 的审批实例统一状态（飞书 PENDING/APPROVED/REJECTED/CANCELED/DELETED/REVERTED + 企微 sp_status 1/2/3/4/6 都映射到这里）。
/// </summary>
public enum ExternalApprovalStatus
{
    Unknown = 0,
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Canceled = 4,
    Deleted = 5,
    Reverted = 6,
    Transferred = 7,
}
