using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.ExternalConnectors.Enums;

namespace Atlas.Domain.ExternalConnectors.Entities;

/// <summary>
/// 本地审批表单字段 ↔ 外部模板控件 ID 的映射，按 ApprovalFlowDefinition 维度。
/// 同时承载 IntegrationMode：A(ExternalLed) / B(LocalLed) / C(Hybrid)。
/// </summary>
public sealed class ExternalApprovalTemplateMapping : TenantEntity
{
    public ExternalApprovalTemplateMapping()
        : base(TenantId.Empty)
    {
        ExternalTemplateId = string.Empty;
        FieldMappingJson = string.Empty;
    }

    public ExternalApprovalTemplateMapping(
        TenantId tenantId,
        long id,
        long providerId,
        long flowDefinitionId,
        string externalTemplateId,
        IntegrationMode integrationMode,
        string fieldMappingJson,
        bool enabled,
        DateTimeOffset now)
        : base(tenantId)
    {
        Id = id;
        ProviderId = providerId;
        FlowDefinitionId = flowDefinitionId;
        ExternalTemplateId = externalTemplateId;
        IntegrationMode = integrationMode;
        FieldMappingJson = fieldMappingJson;
        Enabled = enabled;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public long ProviderId { get; private set; }

    public long FlowDefinitionId { get; private set; }

    public string ExternalTemplateId { get; private set; }

    public IntegrationMode IntegrationMode { get; private set; }

    /// <summary>
    /// JSON 数组：[{ "localFieldKey": "...", "externalControlId": "...", "valueType": "string|number|select|file", "transform": "..." }]
    /// </summary>
    public string FieldMappingJson { get; private set; }

    public bool Enabled { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(IntegrationMode integrationMode, string fieldMappingJson, DateTimeOffset now)
    {
        IntegrationMode = integrationMode;
        FieldMappingJson = fieldMappingJson;
        UpdatedAt = now;
    }

    public void Toggle(bool enabled, DateTimeOffset now)
    {
        Enabled = enabled;
        UpdatedAt = now;
    }
}
