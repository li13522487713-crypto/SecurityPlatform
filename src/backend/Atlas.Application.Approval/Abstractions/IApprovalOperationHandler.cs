using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Application.Approval.Abstractions;

/// <summary>
/// 审批流操作处理器接口
/// </summary>
public interface IApprovalOperationHandler
{
    /// <summary>
    /// 支持的操作类型
    /// </summary>
    ApprovalOperationType SupportedOperationType { get; }

    /// <summary>
    /// 执行操作
    /// </summary>
    Task ExecuteAsync(
        TenantId tenantId,
        long instanceId,
        long? taskId,
        long operatorUserId,
        ApprovalOperationRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// 审批操作请求（内部使用）
/// </summary>
public sealed class ApprovalOperationRequest
{
    public ApprovalOperationType OperationType { get; set; }
    public string? Comment { get; set; }
    public string? TargetNodeId { get; set; }
    public string? TargetAssigneeValue { get; set; }
    public List<string>? AdditionalAssigneeValues { get; set; }
    public Dictionary<string, object>? Variables { get; set; }
    public string? IdempotencyKey { get; set; }
}
