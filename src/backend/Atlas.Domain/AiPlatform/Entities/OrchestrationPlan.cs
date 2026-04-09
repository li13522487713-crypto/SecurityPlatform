using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.AiPlatform.Entities;

public enum OrchestrationPlanStatus
{
    Draft = 0,
    Ready = 1,
    Published = 2,
    Archived = 3
}

public sealed class OrchestrationPlan : TenantEntity
{
    public OrchestrationPlan()
        : base(TenantId.Empty)
    {
        PlanKey = string.Empty;
        PlanName = string.Empty;
        Description = string.Empty;
        TriggerType = "manual";
        TriggerConfigJson = "{}";
        NodeGraphJson = "{}";
        InputSchemaJson = "{}";
        OutputSchemaJson = "{}";
        RuntimePolicyJson = "{}";
        MetadataJson = "{}";
        Status = OrchestrationPlanStatus.Draft;
        PublishedVersion = 0;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public OrchestrationPlan(
        TenantId tenantId,
        long id,
        long appInstanceId,
        string planKey,
        string planName,
        string? description,
        long createdBy,
        DateTimeOffset now)
        : base(tenantId)
    {
        Id = id;
        AppInstanceId = appInstanceId;
        PlanKey = planKey;
        PlanName = planName;
        Description = description ?? string.Empty;
        TriggerType = "manual";
        TriggerConfigJson = "{}";
        NodeGraphJson = "{}";
        InputSchemaJson = "{}";
        OutputSchemaJson = "{}";
        RuntimePolicyJson = "{}";
        MetadataJson = "{}";
        Status = OrchestrationPlanStatus.Draft;
        PublishedVersion = 0;
        CreatedBy = createdBy;
        UpdatedBy = createdBy;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public long AppInstanceId { get; private set; }

    [SugarColumn(Length = 128)]
    public string PlanKey { get; private set; }

    [SugarColumn(Length = 256)]
    public string PlanName { get; private set; }

    [SugarColumn(Length = 1024, IsNullable = true)]
    public string? Description { get; private set; }

    [SugarColumn(Length = 64)]
    public string TriggerType { get; private set; }

    [SugarColumn(ColumnDataType = "TEXT")]
    public string TriggerConfigJson { get; private set; }

    [SugarColumn(ColumnDataType = "TEXT")]
    public string NodeGraphJson { get; private set; }

    [SugarColumn(ColumnDataType = "TEXT")]
    public string InputSchemaJson { get; private set; }

    [SugarColumn(ColumnDataType = "TEXT")]
    public string OutputSchemaJson { get; private set; }

    [SugarColumn(ColumnDataType = "TEXT")]
    public string RuntimePolicyJson { get; private set; }

    [SugarColumn(ColumnDataType = "TEXT")]
    public string MetadataJson { get; private set; }

    public OrchestrationPlanStatus Status { get; private set; }
    public int PublishedVersion { get; private set; }
    public long CreatedBy { get; private set; }
    public long UpdatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }

    public void Update(
        string planName,
        string? description,
        string triggerType,
        string triggerConfigJson,
        string nodeGraphJson,
        string inputSchemaJson,
        string outputSchemaJson,
        string runtimePolicyJson,
        string metadataJson,
        long updatedBy,
        DateTimeOffset now)
    {
        PlanName = planName;
        Description = description ?? string.Empty;
        TriggerType = string.IsNullOrWhiteSpace(triggerType) ? "manual" : triggerType.Trim();
        TriggerConfigJson = string.IsNullOrWhiteSpace(triggerConfigJson) ? "{}" : triggerConfigJson;
        NodeGraphJson = string.IsNullOrWhiteSpace(nodeGraphJson) ? "{}" : nodeGraphJson;
        InputSchemaJson = string.IsNullOrWhiteSpace(inputSchemaJson) ? "{}" : inputSchemaJson;
        OutputSchemaJson = string.IsNullOrWhiteSpace(outputSchemaJson) ? "{}" : outputSchemaJson;
        RuntimePolicyJson = string.IsNullOrWhiteSpace(runtimePolicyJson) ? "{}" : runtimePolicyJson;
        MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? "{}" : metadataJson;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public void MarkReady(long updatedBy, DateTimeOffset now)
    {
        Status = OrchestrationPlanStatus.Ready;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public void Publish(long updatedBy, DateTimeOffset now)
    {
        Status = OrchestrationPlanStatus.Published;
        PublishedVersion++;
        PublishedAt = now;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public void Archive(long updatedBy, DateTimeOffset now)
    {
        Status = OrchestrationPlanStatus.Archived;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }
}
