using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.WorkflowCore.Models;

namespace Atlas.Domain.Workflow.Entities;

public sealed class PersistedExecutionPointer : TenantEntity
{
    public PersistedExecutionPointer()
        : base(TenantId.Empty)
    {
        WorkflowId = string.Empty;
        PointerId = string.Empty;
        StepName = null;
        EventName = null;
        EventKey = null;
        PersistenceDataJson = null;
        EventDataJson = null;
        ContextItemJson = null;
        OutcomeJson = null;
        PredecessorId = null;
    }

    public PersistedExecutionPointer(TenantId tenantId, string workflowId, string pointerId, int stepId, long id)
        : base(tenantId)
    {
        Id = id;
        WorkflowId = workflowId;
        PointerId = pointerId;
        StepId = stepId;
        Active = true;
        Status = PointerStatus.Pending;
        RetryCount = 0;
        StepName = null;
        EventName = null;
        EventKey = null;
        PersistenceDataJson = null;
        EventDataJson = null;
        ContextItemJson = null;
        OutcomeJson = null;
        PredecessorId = null;
    }

    public string WorkflowId { get; private set; }

    public string PointerId { get; private set; }

    public int StepId { get; private set; }

    public bool Active { get; private set; }

    public DateTimeOffset? SleepUntil { get; private set; }

    public string? PersistenceDataJson { get; private set; }

    public DateTimeOffset? StartTime { get; private set; }

    public DateTimeOffset? EndTime { get; private set; }

    public string? EventName { get; private set; }

    public string? EventKey { get; private set; }

    public bool EventPublished { get; private set; }

    public string? EventDataJson { get; private set; }

    public string? StepName { get; private set; }

    public int RetryCount { get; private set; }

    public string? ContextItemJson { get; private set; }

    public string? PredecessorId { get; private set; }

    public string? OutcomeJson { get; private set; }

    public PointerStatus Status { get; private set; }

    public void UpdateStatus(PointerStatus status)
    {
        Status = status;
    }

    public void MarkActive(bool active)
    {
        Active = active;
    }

    public void SetSleepUntil(DateTimeOffset? sleepUntil)
    {
        SleepUntil = sleepUntil;
    }

    public void SetPersistenceDataJson(string? persistenceDataJson)
    {
        PersistenceDataJson = persistenceDataJson;
    }

    public void SetStartTime(DateTimeOffset? startTime)
    {
        StartTime = startTime;
    }

    public void SetEndTime(DateTimeOffset? endTime)
    {
        EndTime = endTime;
    }

    public void SetEventInfo(string? eventName, string? eventKey, bool published)
    {
        EventName = eventName;
        EventKey = eventKey;
        EventPublished = published;
    }

    public void SetEventDataJson(string? eventDataJson)
    {
        EventDataJson = eventDataJson;
    }

    public void SetStepName(string? stepName)
    {
        StepName = stepName;
    }

    public void SetRetryCount(int retryCount)
    {
        RetryCount = retryCount;
    }

    public void IncrementRetryCount()
    {
        RetryCount++;
    }

    public void SetContextItemJson(string? contextItemJson)
    {
        ContextItemJson = contextItemJson;
    }

    public void SetPredecessorId(string? predecessorId)
    {
        PredecessorId = predecessorId;
    }

    public void SetOutcomeJson(string? outcomeJson)
    {
        OutcomeJson = outcomeJson;
    }
}
