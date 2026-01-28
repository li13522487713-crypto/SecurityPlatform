namespace Atlas.Application.Approval.Models;

/// <summary>
/// 抄送记录响应
/// </summary>
public record ApprovalCopyRecordResponse
{
    /// <summary>抄送记录 ID</summary>
    public required long Id { get; init; }

    /// <summary>流程实例 ID</summary>
    public required long InstanceId { get; init; }

    /// <summary>抄送节点 ID</summary>
    public required string NodeId { get; init; }

    /// <summary>收件人用户 ID</summary>
    public required long RecipientUserId { get; init; }

    /// <summary>是否已读</summary>
    public required bool IsRead { get; init; }

    /// <summary>创建时间</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>阅读时间</summary>
    public DateTimeOffset? ReadAt { get; init; }
}
