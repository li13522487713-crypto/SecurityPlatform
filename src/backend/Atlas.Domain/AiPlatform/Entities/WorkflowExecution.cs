using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Enums;
using SqlSugar;

namespace Atlas.Domain.AiPlatform.Entities;

/// <summary>
/// V2 工作流执行实例记录。
/// </summary>
public sealed class WorkflowExecution : TenantEntity
{
    /// <summary>SqlSugar 无参构造。</summary>
    public WorkflowExecution() : base(TenantId.Empty) { }

    public WorkflowExecution(
        TenantId tenantId,
        long workflowId,
        int versionNumber,
        long createdByUserId,
        string? inputsJson,
        long id,
        long? appId = null,
        long? releaseId = null,
        long? runtimeContextId = null)
        : base(tenantId)
    {
        Id = id;
        WorkflowId = workflowId;
        VersionNumber = versionNumber;
        CreatedByUserId = createdByUserId;
        InputsJson = inputsJson;
        Status = ExecutionStatus.Pending;
        InterruptType = InterruptType.None;
        StartedAt = DateTime.UtcNow;
        AppId = appId;
        ReleaseId = releaseId;
        RuntimeContextId = runtimeContextId;
    }

    public long WorkflowId { get; private set; }
    public int VersionNumber { get; private set; }
    [SugarColumn(IsNullable = true)]
    public long? AppId { get; private set; }
    [SugarColumn(IsNullable = true)]
    public long? ReleaseId { get; private set; }
    [SugarColumn(IsNullable = true)]
    public long? RuntimeContextId { get; private set; }
    public ExecutionStatus Status { get; private set; }
    [SugarColumn(IsNullable = true)]
    public string? InputsJson { get; private set; }
    [SugarColumn(IsNullable = true)]
    public string? OutputsJson { get; private set; }
    [SugarColumn(IsNullable = true)]
    public string? ErrorMessage { get; private set; }
    public DateTime StartedAt { get; private set; }
    [SugarColumn(IsNullable = true)]
    public DateTime? CompletedAt { get; private set; }
    public long CreatedByUserId { get; private set; }
    public InterruptType InterruptType { get; private set; }
    [SugarColumn(IsNullable = true)]
    public string? InterruptNodeKey { get; private set; }

    public void Start()
    {
        Status = ExecutionStatus.Running;
        StartedAt = DateTime.UtcNow;
    }

    public void Complete(string? outputsJson)
    {
        Status = ExecutionStatus.Completed;
        OutputsJson = outputsJson;
        CompletedAt = DateTime.UtcNow;
    }

    public void Fail(string errorMessage)
    {
        Status = ExecutionStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        Status = ExecutionStatus.Cancelled;
        CompletedAt = DateTime.UtcNow;
    }

    public void Interrupt(InterruptType type, string nodeKey)
    {
        Status = ExecutionStatus.Interrupted;
        InterruptType = type;
        InterruptNodeKey = nodeKey;
    }

    public void Resume()
    {
        Status = ExecutionStatus.Running;
        InterruptType = InterruptType.None;
        InterruptNodeKey = null;
    }

    public void BindRuntimeReferences(
        long? appId,
        long? releaseId,
        long? runtimeContextId)
    {
        AppId = appId;
        ReleaseId = releaseId;
        RuntimeContextId = runtimeContextId;
    }
}
