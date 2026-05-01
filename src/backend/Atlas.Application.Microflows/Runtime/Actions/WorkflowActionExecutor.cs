using System.Diagnostics;
using System.Text.Json;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime.Connectors;

namespace Atlas.Application.Microflows.Runtime.Actions;

public sealed class WorkflowActionExecutor : IMicroflowActionExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IWorkflowRuntimeClient _workflowRuntimeClient;

    public WorkflowActionExecutor(IWorkflowRuntimeClient workflowRuntimeClient)
    {
        _workflowRuntimeClient = workflowRuntimeClient;
    }

    public string ActionKind => "callWorkflow";

    public string Category => MicroflowActionRuntimeCategory.ConnectorBacked;

    public string SupportLevel => _workflowRuntimeClient.GetCapabilityStatus().Available
        ? MicroflowActionSupportLevel.Supported
        : MicroflowActionSupportLevel.RequiresConnector;

    public async Task<MicroflowActionExecutionResult> ExecuteAsync(MicroflowActionExecutionContext context, CancellationToken ct)
    {
        var started = Stopwatch.StartNew();
        try
        {
            return context.ActionKind switch
            {
                "callWorkflow" => await ExecuteCallWorkflowAsync(context, started, ct).ConfigureAwait(false),
                "changeWorkflowState" => await ExecuteChangeWorkflowStateAsync(context, started, ct).ConfigureAwait(false),
                "completeUserTask" => await ExecuteCompleteUserTaskAsync(context, started, ct).ConfigureAwait(false),
                _ => Failed(RuntimeErrorCode.RuntimeConnectorRequired, $"Workflow action '{context.ActionKind}' is not implemented.", context, started)
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Failed(RuntimeErrorCode.RuntimeConnectorRequired, ex.Message, context, started);
        }
    }

    private async Task<MicroflowActionExecutionResult> ExecuteCallWorkflowAsync(
        MicroflowActionExecutionContext context,
        Stopwatch started,
        CancellationToken ct)
    {
        var workflowId = ReadString(context.ActionConfig, "workflowId")
            ?? ReadString(context.ActionConfig, "targetWorkflowId")
            ?? ReadStringByPath(context.ActionConfig, "workflow", "id");
        if (string.IsNullOrWhiteSpace(workflowId))
        {
            return Failed(RuntimeErrorCode.RuntimeConnectorRequired, "callWorkflow requires workflowId.", context, started);
        }

        var version = ReadInt(context.ActionConfig, "version");
        var reference = ReadString(context.ActionConfig, "reference");
        var payload = ReadOptionalJson(context.ActionConfig, "data")
            ?? ReadOptionalJson(context.ActionConfig, "input")
            ?? ReadOptionalJsonByPath(context.ActionConfig, "workflow", "data");
        var result = await _workflowRuntimeClient.StartWorkflowAsync(workflowId!, version, payload, reference, ct).ConfigureAwait(false);
        if (!result.Success)
        {
            return Failed(RuntimeErrorCode.RuntimeConnectorRequired, result.ErrorMessage ?? "Workflow start failed.", context, started);
        }

        return Success(
            new
            {
                workflow = new
                {
                    action = "callWorkflow",
                    workflowId,
                    version,
                    reference,
                    instanceId = result.InstanceId
                }
            },
            context,
            started);
    }

    private async Task<MicroflowActionExecutionResult> ExecuteChangeWorkflowStateAsync(
        MicroflowActionExecutionContext context,
        Stopwatch started,
        CancellationToken ct)
    {
        var instanceId = ReadString(context.ActionConfig, "instanceId")
            ?? ReadString(context.ActionConfig, "workflowInstanceId");
        var state = ReadString(context.ActionConfig, "state")
            ?? ReadString(context.ActionConfig, "targetState")
            ?? "resume";
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return Failed(RuntimeErrorCode.RuntimeConnectorRequired, "changeWorkflowState requires instanceId.", context, started);
        }

        WorkflowRuntimeCommandResult result = state.ToLowerInvariant() switch
        {
            "suspend" => await _workflowRuntimeClient.SuspendWorkflowAsync(instanceId!, ct).ConfigureAwait(false),
            "terminate" => await _workflowRuntimeClient.TerminateWorkflowAsync(instanceId!, ct).ConfigureAwait(false),
            _ => await _workflowRuntimeClient.ResumeWorkflowAsync(instanceId!, ct).ConfigureAwait(false)
        };
        if (!result.Success)
        {
            return Failed(RuntimeErrorCode.RuntimeConnectorRequired, result.ErrorMessage ?? "Workflow state change failed.", context, started);
        }

        return Success(
            new
            {
                workflow = new
                {
                    action = "changeWorkflowState",
                    instanceId,
                    state
                }
            },
            context,
            started);
    }

    private async Task<MicroflowActionExecutionResult> ExecuteCompleteUserTaskAsync(
        MicroflowActionExecutionContext context,
        Stopwatch started,
        CancellationToken ct)
    {
        var eventKey = ReadString(context.ActionConfig, "taskId")
            ?? ReadString(context.ActionConfig, "userTaskId")
            ?? ReadString(context.ActionConfig, "eventKey");
        if (string.IsNullOrWhiteSpace(eventKey))
        {
            return Failed(RuntimeErrorCode.RuntimeConnectorRequired, "completeUserTask requires taskId/eventKey.", context, started);
        }

        var eventName = ReadString(context.ActionConfig, "eventName") ?? "userTask.completed";
        var eventData = ReadOptionalJson(context.ActionConfig, "data")
            ?? ReadOptionalJson(context.ActionConfig, "payload");
        var result = await _workflowRuntimeClient.PublishEventAsync(eventName, eventKey!, eventData, ct).ConfigureAwait(false);
        if (!result.Success)
        {
            return Failed(RuntimeErrorCode.RuntimeConnectorRequired, result.ErrorMessage ?? "Workflow event publish failed.", context, started);
        }

        return Success(
            new
            {
                workflow = new
                {
                    action = "completeUserTask",
                    eventName,
                    eventKey
                }
            },
            context,
            started);
    }

    private static MicroflowActionExecutionResult Success(object payload, MicroflowActionExecutionContext context, Stopwatch started)
    {
        started.Stop();
        return new MicroflowActionExecutionResult
        {
            Status = MicroflowActionExecutionStatus.Success,
            OutputJson = JsonSerializer.SerializeToElement(payload, JsonOptions),
            OutputPreview = $"{context.ActionKind} delegated to workflow runtime.",
            DurationMs = (int)started.ElapsedMilliseconds
        };
    }

    private static MicroflowActionExecutionResult Failed(
        string code,
        string message,
        MicroflowActionExecutionContext context,
        Stopwatch started)
    {
        started.Stop();
        return new MicroflowActionExecutionResult
        {
            Status = MicroflowActionExecutionStatus.Failed,
            Error = new MicroflowRuntimeErrorDto
            {
                Code = code,
                Message = message,
                ObjectId = context.ObjectId,
                ActionId = context.ActionId
            },
            Message = message,
            DurationMs = (int)started.ElapsedMilliseconds,
            ShouldStopRun = true
        };
    }

    private static string? ReadString(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty(propertyName, out var value)
           && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? ReadInt(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty(propertyName, out var value)
           && value.TryGetInt32(out var parsed)
            ? parsed
            : null;

    private static JsonElement? ReadOptionalJson(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var value)
            ? value.Clone()
            : null;

    private static JsonElement? ReadOptionalJsonByPath(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current.Clone();
    }

    private static string? ReadStringByPath(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }
}
