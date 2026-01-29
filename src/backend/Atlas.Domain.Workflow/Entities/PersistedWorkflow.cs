using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.WorkflowCore.Models;

namespace Atlas.Domain.Workflow.Entities;

public sealed class PersistedWorkflow : TenantEntity
{
    public PersistedWorkflow()
        : base(TenantId.Empty)
    {
        WorkflowDefinitionId = string.Empty;
        Description = null;
        Reference = null;
        DataJson = null;
    }

    public PersistedWorkflow(TenantId tenantId, string workflowDefinitionId, int version, long id)
        : base(tenantId)
    {
        Id = id;
        WorkflowDefinitionId = workflowDefinitionId;
        Version = version;
        Status = WorkflowStatus.Runnable;
        CreateTime = DateTimeOffset.UtcNow;
        Description = null;
        Reference = null;
        DataJson = null;
    }

    public string WorkflowDefinitionId { get; private set; }

    public int Version { get; private set; }

    public string? Description { get; private set; }

    public string? Reference { get; private set; }

    public WorkflowStatus Status { get; private set; }

    public string? DataJson { get; private set; }

    public DateTimeOffset CreateTime { get; private set; }

    public DateTimeOffset? CompleteTime { get; private set; }

    public long? NextExecution { get; private set; }

    public void SetDescription(string? description)
    {
        Description = description;
    }

    public void SetReference(string? reference)
    {
        Reference = reference;
    }

    public void SetDataJson(string? dataJson)
    {
        DataJson = dataJson;
    }

    public void SetCreateTime(DateTimeOffset createTime)
    {
        CreateTime = createTime;
    }

    public void UpdateStatus(WorkflowStatus status)
    {
        Status = status;
    }

    public void SetCompleteTime(DateTimeOffset? completeTime)
    {
        CompleteTime = completeTime;
    }

    public void UpdateNextExecution(long? nextExecution)
    {
        NextExecution = nextExecution;
    }
}
