using Atlas.Domain.Approval.Enums;

namespace Atlas.Application.Approval.Models;

/// <summary>
/// 审批流程实例列表项
/// </summary>
public record ApprovalInstanceListItem
{
    /// <summary>实例 ID</summary>
    public required long Id { get; init; }

    /// <summary>定义 ID</summary>
    public required long DefinitionId { get; init; }

    /// <summary>流程名称</summary>
    public required string FlowName { get; init; }

    /// <summary>业务 key</summary>
    public required string BusinessKey { get; init; }

    /// <summary>发起人 ID</summary>
    public required long InitiatorUserId { get; init; }

    /// <summary>实例状态</summary>
    public required ApprovalInstanceStatus Status { get; init; }

    /// <summary>启动时间</summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>结束时间</summary>
    public DateTimeOffset? EndedAt { get; init; }

    /// <summary>当前节点名称</summary>
    public string? CurrentNodeName { get; init; }

    /// <summary>SLA 剩余分钟（负值表示已超时）</summary>
    public int? SlaRemainingMinutes { get; init; }

    /// <summary>业务数据 JSON（表单提交数据）</summary>
    public string? DataJson { get; init; }
}
