using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Domain.Approval.Entities;

/// <summary>
/// 审批流操作记录（用于幂等性保护和操作审计）
/// </summary>
public sealed class ApprovalOperationRecord : TenantEntity
{
    public ApprovalOperationRecord()
        : base(TenantId.Empty)
    {
        IdempotencyKey = string.Empty;
        OperationType = ApprovalOperationType.Submit;
        InstanceId = 0;
        TaskId = null;
        OperatorUserId = 0;
        PayloadJson = null;
        Status = ApprovalOperationStatus.Pending;
    }

    public ApprovalOperationRecord(
        TenantId tenantId,
        long instanceId,
        ApprovalOperationType operationType,
        string idempotencyKey,
        long operatorUserId,
        long id,
        long? taskId = null,
        string? payloadJson = null)
        : base(tenantId)
    {
        Id = id;
        InstanceId = instanceId;
        TaskId = taskId;
        OperationType = operationType;
        IdempotencyKey = idempotencyKey;
        OperatorUserId = operatorUserId;
        PayloadJson = payloadJson;
        Status = ApprovalOperationStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
        CompletedAt = null;
        ErrorMessage = null;
    }

    /// <summary>流程实例 ID</summary>
    public long InstanceId { get; private set; }

    /// <summary>任务 ID（可选，某些操作不需要任务）</summary>
    public long? TaskId { get; private set; }

    /// <summary>操作类型</summary>
    public ApprovalOperationType OperationType { get; private set; }

    /// <summary>幂等键（由调用方生成，用于防止重复提交）</summary>
    public string IdempotencyKey { get; private set; }

    /// <summary>操作人用户 ID</summary>
    public long OperatorUserId { get; private set; }

    /// <summary>操作参数 JSON（如转办目标、加签人员列表等）</summary>
    public string? PayloadJson { get; private set; }

    /// <summary>操作状态</summary>
    public ApprovalOperationStatus Status { get; private set; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>完成时间</summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>错误消息（如果操作失败）</summary>
    public string? ErrorMessage { get; private set; }

    public void MarkCompleted(DateTimeOffset now)
    {
        Status = ApprovalOperationStatus.Completed;
        CompletedAt = now;
    }

    public void MarkFailed(string errorMessage, DateTimeOffset now)
    {
        Status = ApprovalOperationStatus.Failed;
        CompletedAt = now;
        ErrorMessage = errorMessage;
    }
}

/// <summary>
/// 操作状态
/// </summary>
public enum ApprovalOperationStatus
{
    /// <summary>处理中</summary>
    Pending = 0,

    /// <summary>已完成</summary>
    Completed = 1,

    /// <summary>失败</summary>
    Failed = 2
}
