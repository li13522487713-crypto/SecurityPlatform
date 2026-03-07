using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Enums;
using SqlSugar;

namespace Atlas.Domain.Approval.Entities;

/// <summary>
/// 审批历史事件（每次状态推进的记录）
/// </summary>
[SugarIndex(
    "IX_ApprovalHistoryEvent_TenantId_InstanceId",
    nameof(TenantIdValue), OrderByType.Asc,
    nameof(InstanceId), OrderByType.Asc)]
public sealed class ApprovalHistoryEvent : TenantEntity
{
    public ApprovalHistoryEvent()
        : base(TenantId.Empty)
    {
        FromNode = null;
        ToNode = null;
        PayloadJson = null;
    }

    public ApprovalHistoryEvent(
        TenantId tenantId,
        long instanceId,
        ApprovalHistoryEventType eventType,
        string? fromNode,
        string? toNode,
        long? actorUserId,
        long id,
        string? payloadJson = null)
        : base(tenantId)
    {
        Id = id;
        InstanceId = instanceId;
        EventType = eventType;
        FromNode = fromNode;
        ToNode = toNode;
        PayloadJson = payloadJson;
        ActorUserId = actorUserId;
        OccurredAt = DateTimeOffset.UtcNow;
    }

    /// <summary>实例 ID</summary>
    public long InstanceId { get; private set; }

    /// <summary>事件类型</summary>
    public ApprovalHistoryEventType EventType { get; private set; }

    /// <summary>源节点 ID</summary>
    public string? FromNode { get; private set; }

    /// <summary>目标节点 ID</summary>
    public string? ToNode { get; private set; }

    /// <summary>事件关联的数据 JSON（如审批人、意见等）</summary>
    public string? PayloadJson { get; private set; }

    /// <summary>操作人 ID</summary>
    public long? ActorUserId { get; private set; }

    /// <summary>事件发生时间</summary>
    public DateTimeOffset OccurredAt { get; private set; }
}
