using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.ExternalConnectors.Enums;

namespace Atlas.Domain.ExternalConnectors.Entities;

/// <summary>
/// 本地 ApprovalProcessInstance ↔ 外部 sp_no / instance_code 的关联。
/// 同时记录最后一次状态同步时间与最近一次的卡片消息，便于"已通过/已拒绝"卡片更新。
/// </summary>
public sealed class ExternalApprovalInstanceLink : TenantEntity
{
    public ExternalApprovalInstanceLink()
        : base(TenantId.Empty)
    {
        ExternalInstanceId = string.Empty;
        ExternalTemplateId = string.Empty;
    }

    public ExternalApprovalInstanceLink(
        TenantId tenantId,
        long id,
        long providerId,
        long localInstanceId,
        string externalInstanceId,
        string externalTemplateId,
        IntegrationMode integrationMode,
        DateTimeOffset now)
        : base(tenantId)
    {
        Id = id;
        ProviderId = providerId;
        LocalInstanceId = localInstanceId;
        ExternalInstanceId = externalInstanceId;
        ExternalTemplateId = externalTemplateId;
        IntegrationMode = integrationMode;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public long ProviderId { get; private set; }

    public long LocalInstanceId { get; private set; }

    public string ExternalInstanceId { get; private set; }

    public string ExternalTemplateId { get; private set; }

    public IntegrationMode IntegrationMode { get; private set; }

    /// <summary>外部最近一次状态原文（飞书 PENDING/APPROVED/...）。</summary>
    public string? LastExternalStatus { get; private set; }

    /// <summary>外部最近一次状态变更时间。</summary>
    public DateTimeOffset? LastStatusAt { get; private set; }

    /// <summary>最近一次推送的待审批卡片 messageId / response_code（用于 update_template_card）。</summary>
    public string? LastCardMessageId { get; private set; }

    public string? LastCardResponseCode { get; private set; }

    public int LastCardVersion { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void RecordExternalStatus(string status, DateTimeOffset now)
    {
        LastExternalStatus = status;
        LastStatusAt = now;
        UpdatedAt = now;
    }

    public void RecordCardDispatch(string messageId, string? responseCode, int cardVersion, DateTimeOffset now)
    {
        LastCardMessageId = messageId;
        LastCardResponseCode = responseCode;
        LastCardVersion = cardVersion;
        UpdatedAt = now;
    }
}
