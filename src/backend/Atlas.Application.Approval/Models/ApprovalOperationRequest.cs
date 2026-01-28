using Atlas.Domain.Approval.Enums;

namespace Atlas.Application.Approval.Models;

/// <summary>
/// 审批流运行时操作请求
/// </summary>
public sealed class ApprovalOperationRequest
{
    /// <summary>操作类型</summary>
    public ApprovalOperationType OperationType { get; set; }

    /// <summary>操作说明/意见</summary>
    public string? Comment { get; set; }

    /// <summary>目标节点ID（用于退回任意节点）</summary>
    public string? TargetNodeId { get; set; }

    /// <summary>目标处理人值（用于转办、变更处理人）</summary>
    public string? TargetAssigneeValue { get; set; }

    /// <summary>额外审批人列表（用于加签）</summary>
    public List<string>? AdditionalAssigneeValues { get; set; }
}
