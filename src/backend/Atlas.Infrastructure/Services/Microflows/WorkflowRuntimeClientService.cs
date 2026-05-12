using System.Text.Json;
using Atlas.Application.Microflows.Runtime.Connectors;
using Atlas.Application.Workflow.Abstractions;
using Atlas.Application.Workflow.Models;

namespace Atlas.Infrastructure.Services.Microflows;

public sealed class WorkflowRuntimeClientService : IWorkflowRuntimeClient
{
    private readonly IWorkflowCommandService _workflowCommandService;

    public WorkflowRuntimeClientService(IWorkflowCommandService workflowCommandService)
    {
        _workflowCommandService = workflowCommandService;
    }

    public MicroflowConnectorCapabilityStatus GetCapabilityStatus()
        => new("workflow.action", true, "available");

    public async Task<WorkflowRuntimeStartResult> StartWorkflowAsync(
        string workflowId,
        int? version,
        JsonElement? data,
        string? reference,
        CancellationToken cancellationToken)
    {
        var instanceId = await _workflowCommandService.StartWorkflowAsync(
            new StartWorkflowRequest
            {
                WorkflowId = workflowId,
                Version = version,
                Reference = reference,
                Data = data.HasValue ? JsonSerializer.Deserialize<object>(data.Value.GetRawText()) : null
            },
            cancellationToken).ConfigureAwait(false);

        return new WorkflowRuntimeStartResult
        {
            Success = !string.IsNullOrWhiteSpace(instanceId),
            InstanceId = instanceId
        };
    }

    public async Task<WorkflowRuntimeCommandResult> SuspendWorkflowAsync(string instanceId, CancellationToken cancellationToken)
        => new()
        {
            Success = await _workflowCommandService.SuspendWorkflowAsync(instanceId, cancellationToken).ConfigureAwait(false)
        };

    public async Task<WorkflowRuntimeCommandResult> ResumeWorkflowAsync(string instanceId, CancellationToken cancellationToken)
        => new()
        {
            Success = await _workflowCommandService.ResumeWorkflowAsync(instanceId, cancellationToken).ConfigureAwait(false)
        };

    public async Task<WorkflowRuntimeCommandResult> TerminateWorkflowAsync(string instanceId, CancellationToken cancellationToken)
        => new()
        {
            Success = await _workflowCommandService.TerminateWorkflowAsync(instanceId, cancellationToken).ConfigureAwait(false)
        };

    public async Task<WorkflowRuntimeCommandResult> PublishEventAsync(
        string eventName,
        string eventKey,
        JsonElement? eventData,
        CancellationToken cancellationToken)
    {
        await _workflowCommandService.PublishEventAsync(
            new PublishEventRequest
            {
                EventName = eventName,
                EventKey = eventKey,
                EventData = eventData.HasValue ? JsonSerializer.Deserialize<object>(eventData.Value.GetRawText()) : null
            },
            cancellationToken).ConfigureAwait(false);
        return new WorkflowRuntimeCommandResult
        {
            Success = true
        };
    }
}
