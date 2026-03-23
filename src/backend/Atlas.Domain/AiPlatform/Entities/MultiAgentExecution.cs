using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class MultiAgentExecution : TenantEntity
{
    public MultiAgentExecution()
        : base(TenantId.Empty)
    {
        InputMessage = string.Empty;
        OutputMessage = string.Empty;
        TraceJson = "[]";
        ErrorMessage = string.Empty;
        StartedAt = DateTime.UtcNow;
        CompletedAt = DateTime.UnixEpoch;
        Status = ExecutionStatus.Pending;
    }

    public MultiAgentExecution(
        TenantId tenantId,
        long orchestrationId,
        long triggeredByUserId,
        string inputMessage,
        long id)
        : base(tenantId)
    {
        Id = id;
        OrchestrationId = orchestrationId;
        TriggeredByUserId = triggeredByUserId;
        InputMessage = inputMessage;
        OutputMessage = string.Empty;
        TraceJson = "[]";
        ErrorMessage = string.Empty;
        StartedAt = DateTime.UtcNow;
        CompletedAt = DateTime.UnixEpoch;
        Status = ExecutionStatus.Pending;
    }

    public long OrchestrationId { get; private set; }
    public long TriggeredByUserId { get; private set; }
    public ExecutionStatus Status { get; private set; }
    public string InputMessage { get; private set; }
    public string? OutputMessage { get; private set; }
    public string TraceJson { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    public void MarkRunning()
    {
        Status = ExecutionStatus.Running;
    }

    public void MarkCompleted(string outputMessage, string traceJson)
    {
        Status = ExecutionStatus.Completed;
        OutputMessage = outputMessage;
        TraceJson = string.IsNullOrWhiteSpace(traceJson) ? "[]" : traceJson;
        ErrorMessage = null;
        CompletedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string? errorMessage, string traceJson)
    {
        Status = ExecutionStatus.Failed;
        ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? "执行失败" : errorMessage;
        TraceJson = string.IsNullOrWhiteSpace(traceJson) ? "[]" : traceJson;
        CompletedAt = DateTime.UtcNow;
    }

    public void MarkCancelled(string traceJson)
    {
        Status = ExecutionStatus.Cancelled;
        TraceJson = string.IsNullOrWhiteSpace(traceJson) ? "[]" : traceJson;
        CompletedAt = DateTime.UtcNow;
    }
}
