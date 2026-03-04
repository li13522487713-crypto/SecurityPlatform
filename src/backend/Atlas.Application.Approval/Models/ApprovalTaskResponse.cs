using Atlas.Domain.Approval.Enums;

namespace Atlas.Application.Approval.Models;

/// <summary>
/// 审批任务响应
/// </summary>
public record ApprovalTaskResponse
{
    /// <summary>任务 ID</summary>
    public required long Id { get; init; }

    /// <summary>实例 ID</summary>
    public required long InstanceId { get; init; }

    /// <summary>节点 ID</summary>
    public required string NodeId { get; init; }

    /// <summary>任务标题</summary>
    public required string Title { get; init; }

    /// <summary>审批人类型</summary>
    public required AssigneeType AssigneeType { get; init; }

    /// <summary>审批人值</summary>
    public required string AssigneeValue { get; init; }

    /// <summary>任务状态</summary>
    public required ApprovalTaskStatus Status { get; init; }

    /// <summary>决策人 ID</summary>
    public long? DecisionByUserId { get; init; }

    /// <summary>决策时间</summary>
    public DateTimeOffset? DecisionAt { get; init; }

    /// <summary>审批意见</summary>
    public string? Comment { get; init; }

    /// <summary>创建时间</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>流程名称</summary>
    public string? FlowName { get; init; }

    /// <summary>当前节点名称</summary>
    public string? CurrentNodeName { get; init; }

    /// <summary>SLA 剩余分钟（负值表示已超时）</summary>
    public int? SlaRemainingMinutes { get; init; }

    /// <summary>SLA 预期完成时间</summary>
    public DateTimeOffset? ExpectedCompleteTime { get; init; }
}
