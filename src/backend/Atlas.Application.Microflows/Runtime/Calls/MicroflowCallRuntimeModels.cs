using System.Text.Json;
using System.Text.Json.Serialization;
using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Runtime.Calls;

public static class MicroflowCallMode
{
    public const string Sync = "sync";
    public const string AsyncReserved = "asyncReserved";
}

public static class MicroflowCallStackFrameStatus
{
    public const string Entering = "entering";
    public const string Running = "running";
    public const string Success = "success";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
    public const string MaxDepthExceeded = "maxDepthExceeded";
    public const string RecursionDetected = "recursionDetected";
}

public static class MicroflowCallTransactionBoundary
{
    public const string Inherit = "inherit";
    public const string SharedTransaction = "sharedTransaction";
    public const string ChildTransaction = "childTransaction";
    public const string NoTransaction = "noTransaction";
}

public sealed record MicroflowCallDiagnostic
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; init; } = "info";

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("fieldPath")]
    public string? FieldPath { get; init; }
}

public sealed record MicroflowCallParameterBinding
{
    [JsonPropertyName("parameterName")]
    public string ParameterName { get; init; } = string.Empty;

    [JsonPropertyName("parameterTypeJson")]
    public string? ParameterTypeJson { get; init; }

    [JsonPropertyName("argumentExpression")]
    public string? ArgumentExpression { get; init; }

    [JsonPropertyName("valueJson")]
    public string? ValueJson { get; init; }

    [JsonPropertyName("valuePreview")]
    public string? ValuePreview { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = "pending";

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<MicroflowCallDiagnostic> Diagnostics { get; init; } = Array.Empty<MicroflowCallDiagnostic>();
}

public sealed record MicroflowCallReturnBinding
{
    [JsonPropertyName("storeResult")]
    public bool StoreResult { get; init; }

    [JsonPropertyName("outputVariableName")]
    public string? OutputVariableName { get; init; }

    [JsonPropertyName("returnTypeJson")]
    public string? ReturnTypeJson { get; init; }

    [JsonPropertyName("valueJson")]
    public string? ValueJson { get; init; }

    [JsonPropertyName("valuePreview")]
    public string? ValuePreview { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = "pending";

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<MicroflowCallDiagnostic> Diagnostics { get; init; } = Array.Empty<MicroflowCallDiagnostic>();
}

public sealed record MicroflowCallTraceLink
{
    [JsonPropertyName("parentRunId")]
    public string? ParentRunId { get; init; }

    [JsonPropertyName("childRunId")]
    public string? ChildRunId { get; init; }

    [JsonPropertyName("callFrameId")]
    public string? CallFrameId { get; init; }

    [JsonPropertyName("childTraceRootFrameId")]
    public string? ChildTraceRootFrameId { get; init; }
}

public sealed record MicroflowCallStackFrame
{
    [JsonPropertyName("frameId")]
    public string FrameId { get; init; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("parentFrameId")]
    public string? ParentFrameId { get; init; }

    [JsonPropertyName("depth")]
    public int Depth { get; init; }

    [JsonPropertyName("callerResourceId")]
    public string? CallerResourceId { get; init; }

    [JsonPropertyName("callerSchemaId")]
    public string? CallerSchemaId { get; init; }

    [JsonPropertyName("callerObjectId")]
    public string? CallerObjectId { get; init; }

    [JsonPropertyName("callerActionId")]
    public string? CallerActionId { get; init; }

    [JsonPropertyName("callerTraceFrameId")]
    public string? CallerTraceFrameId { get; set; }

    [JsonPropertyName("targetResourceId")]
    public string? TargetResourceId { get; init; }

    [JsonPropertyName("targetSchemaId")]
    public string? TargetSchemaId { get; set; }

    [JsonPropertyName("targetVersion")]
    public string? TargetVersion { get; set; }

    [JsonPropertyName("targetQualifiedName")]
    public string? TargetQualifiedName { get; init; }

    [JsonPropertyName("callMode")]
    public string CallMode { get; init; } = MicroflowCallMode.Sync;

    [JsonPropertyName("status")]
    public string Status { get; set; } = MicroflowCallStackFrameStatus.Entering;

    [JsonPropertyName("startedAt")]
    public DateTimeOffset StartedAt { get; init; }

    [JsonPropertyName("endedAt")]
    public DateTimeOffset? EndedAt { get; set; }

    [JsonPropertyName("durationMs")]
    public int DurationMs { get; set; }

    [JsonPropertyName("parameterBindings")]
    public IReadOnlyList<MicroflowCallParameterBinding> ParameterBindings { get; set; } = Array.Empty<MicroflowCallParameterBinding>();

    [JsonPropertyName("returnBinding")]
    public MicroflowCallReturnBinding? ReturnBinding { get; set; }

    [JsonPropertyName("childRunId")]
    public string? ChildRunId { get; set; }

    [JsonPropertyName("childTraceRootFrameId")]
    public string? ChildTraceRootFrameId { get; set; }

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<MicroflowCallDiagnostic> Diagnostics { get; set; } = Array.Empty<MicroflowCallDiagnostic>();

    [JsonPropertyName("error")]
    public MicroflowRuntimeErrorDto? Error { get; set; }
}

public sealed record MicroflowCallMicroflowRequest
{
    public RuntimeExecutionContext ParentContext { get; init; } = null!;
    public JsonElement ActionConfig { get; init; }
    public string CallerObjectId { get; init; } = string.Empty;
    public string? CallerActionId { get; init; }
    public string? CollectionId { get; init; }
    public string Mode { get; init; } = MicroflowRuntimeExecutionMode.TestRun;
    public int MaxCallDepth { get; init; } = 10;
}

public sealed record MicroflowChildExecutionContext
{
    public RuntimeExecutionContext RuntimeContext { get; init; } = null!;
    public MicroflowCallStackFrame CallFrame { get; init; } = null!;
    public MicroflowExecutionPlan ExecutionPlan { get; init; } = new();
}

public sealed record MicroflowCallMicroflowResult
{
    public bool Success { get; init; }
    public MicroflowCallStackFrame? Frame { get; init; }
    public MicroflowCallTraceLink? TraceLink { get; init; }
    public MicroflowRunSessionDto? ChildSession { get; init; }
    public IReadOnlyList<MicroflowRunSessionDto> ChildRunSessions { get; init; } = Array.Empty<MicroflowRunSessionDto>();
    public IReadOnlyList<MicroflowCallParameterBinding> ParameterBindings { get; init; } = Array.Empty<MicroflowCallParameterBinding>();
    public MicroflowCallReturnBinding? ReturnBinding { get; init; }
    public IReadOnlyList<MicroflowCallDiagnostic> Diagnostics { get; init; } = Array.Empty<MicroflowCallDiagnostic>();
    public JsonElement? OutputJson { get; init; }
    public string? OutputPreview { get; init; }
    public MicroflowRuntimeErrorDto? Error { get; init; }
}
