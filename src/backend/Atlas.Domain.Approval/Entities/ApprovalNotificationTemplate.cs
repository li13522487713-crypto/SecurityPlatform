using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Domain.Approval.Entities;

/// <summary>
/// 审批流程通知模板（对应 AntFlow 的 t_bpmn_conf_notice_template）
/// </summary>
public sealed class ApprovalNotificationTemplate : TenantEntity
{
    public ApprovalNotificationTemplate()
        : base(TenantId.Empty)
    {
        FlowDefinitionId = 0;
        EventType = ApprovalNotificationEventType.TaskCreated;
        Channel = ApprovalNotificationChannel.Inbox;
        TitleTemplate = string.Empty;
        ContentTemplate = string.Empty;
    }

    public ApprovalNotificationTemplate(
        TenantId tenantId,
        long flowDefinitionId,
        ApprovalNotificationEventType eventType,
        ApprovalNotificationChannel channel,
        string titleTemplate,
        string contentTemplate,
        long id)
        : base(tenantId)
    {
        Id = id;
        FlowDefinitionId = flowDefinitionId;
        EventType = eventType;
        Channel = channel;
        TitleTemplate = titleTemplate;
        ContentTemplate = contentTemplate;
        IsEnabled = true;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>流程定义 ID（0 表示系统级模板）</summary>
    public long FlowDefinitionId { get; private set; }

    /// <summary>事件类型</summary>
    public ApprovalNotificationEventType EventType { get; private set; }

    /// <summary>通知渠道</summary>
    public ApprovalNotificationChannel Channel { get; private set; }

    /// <summary>标题模板（支持变量替换，如 {FlowName}, {InitiatorName}, {TaskTitle}）</summary>
    public string TitleTemplate { get; private set; }

    /// <summary>内容模板（支持变量替换）</summary>
    public string ContentTemplate { get; private set; }

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; private set; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>更新时间</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    public void UpdateTemplate(string titleTemplate, string contentTemplate, DateTimeOffset now)
    {
        TitleTemplate = titleTemplate;
        ContentTemplate = contentTemplate;
        UpdatedAt = now;
    }

    public void Enable()
    {
        IsEnabled = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Disable()
    {
        IsEnabled = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
