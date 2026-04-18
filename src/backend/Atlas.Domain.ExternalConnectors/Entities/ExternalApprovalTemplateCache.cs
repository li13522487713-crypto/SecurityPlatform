using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.ExternalConnectors.Entities;

/// <summary>
/// 外部审批模板缓存（企微 getapprovaltmp / 飞书 approval/v4/approvals/{code} 的快照）。
/// 字段映射设计器直接读这张表，避免每次都打外部 API。
/// </summary>
public sealed class ExternalApprovalTemplateCache : TenantEntity
{
    public ExternalApprovalTemplateCache()
        : base(TenantId.Empty)
    {
        ExternalTemplateId = string.Empty;
        Name = string.Empty;
        ControlsJson = string.Empty;
    }

    public ExternalApprovalTemplateCache(
        TenantId tenantId,
        long id,
        long providerId,
        string externalTemplateId,
        string name,
        string? description,
        string controlsJson,
        string? rawJson,
        DateTimeOffset now)
        : base(tenantId)
    {
        Id = id;
        ProviderId = providerId;
        ExternalTemplateId = externalTemplateId;
        Name = name;
        Description = description;
        ControlsJson = controlsJson;
        RawJson = rawJson;
        FetchedAt = now;
    }

    public long ProviderId { get; private set; }

    public string ExternalTemplateId { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    /// <summary>ExternalApprovalTemplateControl[] 的 JSON 序列化结果。</summary>
    public string ControlsJson { get; private set; }

    public string? RawJson { get; private set; }

    public DateTimeOffset FetchedAt { get; private set; }

    public void Refresh(string name, string? description, string controlsJson, string? rawJson, DateTimeOffset now)
    {
        Name = name;
        Description = description;
        ControlsJson = controlsJson;
        RawJson = rawJson;
        FetchedAt = now;
    }
}
