using System.Text.Json;
using System.Text.Json.Serialization;
using Atlas.Application.Microflows.Runtime;

namespace Atlas.Application.Microflows.Models;

public static class MicroflowNavigationMode
{
    public const string DryRun = "dryRun";
    public const string TestRun = "testRun";
    public const string ValidateOnly = "validateOnly";
    public const string Debug = "debug";
}

public static class MicroflowNavigationStatus
{
    public const string Success = "success";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
    public const string MaxStepsExceeded = "maxStepsExceeded";
}

public static class MicroflowNavigationStepStatus
{
    public const string Success = "success";
    public const string Failed = "failed";
    public const string Skipped = "skipped";
    public const string Ignored = "ignored";
}

public sealed record NavigateMicroflowRuntimeRequestDto
{
    [JsonPropertyName("schema")]
    public JsonElement Schema { get; init; }

    [JsonPropertyName("options")]
    public MicroflowNavigationOptions? Options { get; init; }
}

public sealed record MicroflowNavigationOptions
{
    public const int DefaultMaxSteps = 500;

    [JsonPropertyName("mode")]
    public string Mode { get; init; } = MicroflowNavigationMode.DryRun;

    [JsonPropertyName("maxSteps")]
    public int? MaxSteps { get; init; }

    [JsonPropertyName("decisionBooleanResult")]
    public bool? DecisionBooleanResult { get; init; }

    [JsonPropertyName("enumerationCaseValue")]
    public string? EnumerationCaseValue { get; init; }

    [JsonPropertyName("objectTypeCase")]
    public string? ObjectTypeCase { get; init; }

    [JsonPropertyName("loopIterations")]
    public int? LoopIterations { get; init; }

    [JsonPropertyName("simulateActionFailureObjectIds")]
    public IReadOnlyList<string> SimulateActionFailureObjectIds { get; init; } = Array.Empty<string>();

    [JsonPropertyName("simulateRestError")]
    public bool SimulateRestError { get; init; }

    [JsonPropertyName("stopOnUnsupported")]
    public bool? StopOnUnsupported { get; init; }

    [JsonPropertyName("stopOnFirstError")]
    public bool StopOnFirstError { get; init; }

    [JsonPropertyName("includeVariableSnapshots")]
    public bool IncludeVariableSnapshots { get; init; }

    [JsonPropertyName("includeDiagnostics")]
    public bool IncludeDiagnostics { get; init; } = true;

    [JsonPropertyName("preferredStartNodeId")]
    public string? PreferredStartNodeId { get; init; }

    [JsonPropertyName("traceId")]
    public string? TraceId { get; init; }

    [JsonIgnore]
    public int EffectiveMaxSteps => MaxSteps.GetValueOrDefault() > 0 ? MaxSteps.GetValueOrDefault() : DefaultMaxSteps;

    [JsonIgnore]
    public bool EffectiveStopOnUnsupported
        => StopOnUnsupported ?? string.Equals(Mode, MicroflowNavigationMode.TestRun, StringComparison.OrdinalIgnoreCase);
}

public sealed record MicroflowNavigationResult
{
    [JsonPropertyName("runId")]
    public string RunId { get; init; } = string.Empty;

    [JsonPropertyName("traceId")]
    public string TraceId { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = MicroflowNavigationStatus.Failed;

    [JsonPropertyName("startedAt")]
    public DateTimeOffset StartedAt { get; init; }

    [JsonPropertyName("endedAt")]
    public DateTimeOffset EndedAt { get; init; }

    [JsonPropertyName("durationMs")]
    public int DurationMs { get; init; }

    [JsonPropertyName("steps")]
    public IReadOnlyList<MicroflowNavigationStep> Steps { get; init; } = Array.Empty<MicroflowNavigationStep>();

    [JsonPropertyName("traceFrames")]
    public IReadOnlyList<MicroflowNavigationTraceFrame> TraceFrames { get; init; } = Array.Empty<MicroflowNavigationTraceFrame>();

    [JsonPropertyName("diagnostics")]
    public MicroflowFlowNavigatorDiagnostics Diagnostics { get; init; } = new();

    [JsonPropertyName("error")]
    public MicroflowNavigationError? Error { get; init; }

    [JsonPropertyName("terminalNodeId")]
    public string? TerminalNodeId { get; init; }

    [JsonPropertyName("visitedNodeIds")]
    public IReadOnlyList<string> VisitedNodeIds { get; init; } = Array.Empty<string>();

    [JsonPropertyName("visitedFlowIds")]
    public IReadOnlyList<string> VisitedFlowIds { get; init; } = Array.Empty<string>();

    [JsonPropertyName("selectedCaseValues")]
    public IReadOnlyDictionary<string, JsonElement> SelectedCaseValues { get; init; } = new Dictionary<string, JsonElement>();

    [JsonPropertyName("maxSteps")]
    public int MaxSteps { get; init; }

    [JsonPropertyName("stepCount")]
    public int StepCount { get; init; }
}

public sealed record MicroflowNavigationStep
{
    [JsonPropertyName("sequence")]
    public int Sequence { get; init; }

    [JsonPropertyName("objectId")]
    public string ObjectId { get; init; } = string.Empty;

    [JsonPropertyName("actionId")]
    public string? ActionId { get; init; }

    [JsonPropertyName("collectionId")]
    public string? CollectionId { get; init; }

    [JsonPropertyName("incomingFlowId")]
    public string? IncomingFlowId { get; init; }

    [JsonPropertyName("outgoingFlowId")]
    public string? OutgoingFlowId { get; init; }

    [JsonPropertyName("nodeKind")]
    public string NodeKind { get; init; } = string.Empty;

    [JsonPropertyName("actionKind")]
    public string? ActionKind { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = MicroflowNavigationStepStatus.Success;

    [JsonPropertyName("selectedCaseValue")]
    public JsonElement? SelectedCaseValue { get; init; }

    [JsonPropertyName("loopIteration")]
    public JsonElement? LoopIteration { get; init; }

    [JsonPropertyName("error")]
    public MicroflowNavigationError? Error { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("variablesSnapshot")]
    public IReadOnlyDictionary<string, MicroflowRuntimeVariableValueDto>? VariablesSnapshot { get; init; }

    [JsonPropertyName("startedAt")]
    public DateTimeOffset StartedAt { get; init; }

    [JsonPropertyName("endedAt")]
    public DateTimeOffset EndedAt { get; init; }

    [JsonPropertyName("durationMs")]
    public int DurationMs { get; init; }
}

public sealed record MicroflowNodeVisitResult
{
    [JsonPropertyName("node")]
    public MicroflowExecutionNode? Node { get; init; }

    [JsonPropertyName("step")]
    public MicroflowNavigationStep? Step { get; init; }

    [JsonPropertyName("terminal")]
    public bool Terminal { get; init; }

    [JsonPropertyName("error")]
    public MicroflowNavigationError? Error { get; init; }
}

public sealed record MicroflowFlowSelectionResult
{
    [JsonPropertyName("flow")]
    public MicroflowExecutionFlow? Flow { get; init; }

    [JsonPropertyName("selectedCaseValue")]
    public JsonElement? SelectedCaseValue { get; init; }

    [JsonPropertyName("selectedCaseText")]
    public string? SelectedCaseText { get; init; }

    [JsonPropertyName("error")]
    public MicroflowNavigationError? Error { get; init; }
}

public sealed record MicroflowNavigationTraceFrame
{
    [JsonPropertyName("sequence")]
    public int Sequence { get; init; }

    [JsonPropertyName("objectId")]
    public string ObjectId { get; init; } = string.Empty;

    [JsonPropertyName("actionId")]
    public string? ActionId { get; init; }

    [JsonPropertyName("collectionId")]
    public string? CollectionId { get; init; }

    [JsonPropertyName("incomingFlowId")]
    public string? IncomingFlowId { get; init; }

    [JsonPropertyName("outgoingFlowId")]
    public string? OutgoingFlowId { get; init; }

    [JsonPropertyName("selectedCaseValue")]
    public JsonElement? SelectedCaseValue { get; init; }

    [JsonPropertyName("loopIteration")]
    public JsonElement? LoopIteration { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = MicroflowNavigationStepStatus.Success;

    [JsonPropertyName("startedAt")]
    public DateTimeOffset StartedAt { get; init; }

    [JsonPropertyName("endedAt")]
    public DateTimeOffset EndedAt { get; init; }

    [JsonPropertyName("durationMs")]
    public int DurationMs { get; init; }

    [JsonPropertyName("error")]
    public MicroflowNavigationError? Error { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("variablesSnapshot")]
    public IReadOnlyDictionary<string, MicroflowRuntimeVariableValueDto>? VariablesSnapshot { get; init; }
}

public sealed record MicroflowNavigationError
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = RuntimeErrorCode.RuntimeUnknownError;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("objectId")]
    public string? ObjectId { get; init; }

    [JsonPropertyName("actionId")]
    public string? ActionId { get; init; }

    [JsonPropertyName("flowId")]
    public string? FlowId { get; init; }

    [JsonPropertyName("details")]
    public string? Details { get; init; }

    [JsonPropertyName("cause")]
    public string? Cause { get; init; }
}

public sealed record MicroflowFlowNavigatorDiagnostics
{
    [JsonPropertyName("items")]
    public IReadOnlyList<MicroflowExecutionDiagnosticDto> Items { get; init; } = Array.Empty<MicroflowExecutionDiagnosticDto>();

    [JsonPropertyName("errorCount")]
    public int ErrorCount { get; init; }

    [JsonPropertyName("warningCount")]
    public int WarningCount { get; init; }
}

public sealed class MicroflowNavigationContext
{
    public MicroflowNavigationContext(
        MicroflowExecutionPlan plan,
        MicroflowNavigationOptions options,
        string runId,
        string traceId,
        CancellationToken cancellationToken)
    {
        Plan = plan;
        Options = options;
        RunId = runId;
        TraceId = traceId;
        CancellationToken = cancellationToken;
        RuntimeContext = RuntimeExecutionContext.Create(
            runId,
            plan,
            options.Mode,
            input: null,
            securityContext: null,
            startedAt: DateTimeOffset.UtcNow);
    }

    public MicroflowExecutionPlan Plan { get; }
    public RuntimeExecutionContext RuntimeContext { get; }
    public MicroflowNavigationOptions Options { get; }
    public string RunId { get; }
    public string TraceId { get; }
    public string? CurrentNodeId { get; set; }
    public string? CurrentFlowId { get; set; }
    public string? CurrentCollectionId { get; set; }
    public string? CurrentLoopObjectId { get; set; }
    public int StepIndex { get; set; }
    public Stack<string> CallStack { get; } = new();
    public Stack<MicroflowNavigationLoopFrame> LoopStack { get; } = new();
    public Stack<MicroflowNavigationErrorFrame> ErrorStack { get; } = new();
    public Stack<IDisposable> ErrorScopeStack { get; } = new();
    public HashSet<string> VisitedNodeIds { get; } = new(StringComparer.Ordinal);
    public HashSet<string> VisitedFlowIds { get; } = new(StringComparer.Ordinal);
    public List<MicroflowNavigationStep> Steps { get; } = [];
    public List<MicroflowExecutionDiagnosticDto> Diagnostics { get; } = [];
    public Dictionary<string, JsonElement> SelectedCaseValues { get; } = new(StringComparer.Ordinal);
    public CancellationToken CancellationToken { get; }
}

public sealed record MicroflowNavigationLoopFrame
{
    [JsonPropertyName("loopObjectId")]
    public string LoopObjectId { get; init; } = string.Empty;

    [JsonPropertyName("collectionId")]
    public string CollectionId { get; init; } = string.Empty;

    [JsonPropertyName("currentIndex")]
    public int CurrentIndex { get; set; }

    [JsonPropertyName("maxIterations")]
    public int MaxIterations { get; init; }

    [JsonPropertyName("breakRequested")]
    public bool BreakRequested { get; set; }

    [JsonPropertyName("continueRequested")]
    public bool ContinueRequested { get; set; }
}

public sealed record MicroflowNavigationErrorFrame
{
    [JsonPropertyName("sourceObjectId")]
    public string SourceObjectId { get; init; } = string.Empty;

    [JsonPropertyName("sourceActionId")]
    public string? SourceActionId { get; init; }

    [JsonPropertyName("error")]
    public MicroflowNavigationError Error { get; init; } = new();

    [JsonPropertyName("errorHandlerFlowId")]
    public string? ErrorHandlerFlowId { get; init; }

    [JsonPropertyName("latestHttpResponse")]
    public JsonElement? LatestHttpResponse { get; init; }
}

public static class MicroflowNavigationTraceMapper
{
    public static IReadOnlyList<MicroflowTraceFrameDto> ToTraceFrames(this MicroflowNavigationResult result)
        => result.Steps.Select(step => step.ToTraceFrameDto(result.RunId)).ToArray();

    public static MicroflowTraceFrameDto ToTraceFrameDto(this MicroflowNavigationStep step, string runId)
        => new()
        {
            Id = $"{runId}-{step.Sequence:D6}",
            RunId = runId,
            ObjectId = step.ObjectId,
            ActionId = step.ActionId,
            CollectionId = step.CollectionId,
            IncomingFlowId = step.IncomingFlowId,
            OutgoingFlowId = step.OutgoingFlowId,
            SelectedCaseValue = step.SelectedCaseValue,
            LoopIteration = step.LoopIteration,
            Status = step.Status,
            StartedAt = step.StartedAt,
            EndedAt = step.EndedAt,
            DurationMs = step.DurationMs,
            Error = step.Error is null ? null : new MicroflowRuntimeErrorDto
            {
                Code = step.Error.Code,
                Message = step.Error.Message,
                ObjectId = step.Error.ObjectId,
                ActionId = step.Error.ActionId,
                FlowId = step.Error.FlowId,
                Details = step.Error.Details,
                Cause = step.Error.Cause
            },
            Message = step.Message,
            VariablesSnapshot = step.VariablesSnapshot,
            ErrorHandlerVisited = !string.IsNullOrWhiteSpace(step.IncomingFlowId) && step.IncomingFlowId.Contains("error", StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrWhiteSpace(step.OutgoingFlowId) && step.OutgoingFlowId.Contains("error", StringComparison.OrdinalIgnoreCase)
        };
}
