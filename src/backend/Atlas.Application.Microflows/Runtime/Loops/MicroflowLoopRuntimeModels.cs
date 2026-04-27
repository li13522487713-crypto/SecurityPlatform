using System.Text.Json;
using System.Text.Json.Serialization;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime.Actions;
using Atlas.Application.Microflows.Runtime.Expressions;
using Atlas.Application.Microflows.Runtime.Transactions;

namespace Atlas.Application.Microflows.Runtime.Loops;

public interface IMicroflowLoopExecutor
{
    Task<MicroflowLoopExecutionResult> ExecuteLoopAsync(
        MicroflowActionExecutionContext context,
        MicroflowExecutionNode loopNode,
        CancellationToken ct);
}

public static class MicroflowLoopExecutionStatus
{
    public const string Success = "success";
    public const string Failed = "failed";
    public const string Break = "break";
    public const string Continue = "continue";
    public const string Cancelled = "cancelled";
    public const string MaxIterationsExceeded = "maxIterationsExceeded";
}

public static class MicroflowLoopControlSignal
{
    public const string None = "none";
    public const string Break = "break";
    public const string Continue = "continue";
}

public static class MicroflowLoopBodyExecutionStatus
{
    public const string Success = "success";
    public const string Failed = "failed";
    public const string Break = "break";
    public const string Continue = "continue";
    public const string Cancelled = "cancelled";
    public const string MaxStepsExceeded = "maxStepsExceeded";
    public const string IterationCompleted = "iterationCompleted";
}

public sealed record MicroflowLoopExecutionOptions
{
    [JsonPropertyName("maxIterations")]
    public int MaxIterations { get; init; } = 100;

    [JsonPropertyName("maxStepsPerIteration")]
    public int? MaxStepsPerIteration { get; init; }

    [JsonPropertyName("loopIterationsOverride")]
    public int? LoopIterationsOverride { get; init; }

    [JsonPropertyName("allowEmptyList")]
    public bool AllowEmptyList { get; init; } = true;

    [JsonPropertyName("failOnNonListSource")]
    public bool FailOnNonListSource { get; init; } = true;

    [JsonPropertyName("failOnNonBooleanWhileCondition")]
    public bool FailOnNonBooleanWhileCondition { get; init; } = true;

    [JsonPropertyName("traceEachIteration")]
    public bool TraceEachIteration { get; init; } = true;

    [JsonPropertyName("includeIteratorSnapshot")]
    public bool IncludeIteratorSnapshot { get; init; } = true;

    [JsonPropertyName("includeTransactionSnapshot")]
    public bool IncludeTransactionSnapshot { get; init; } = true;

    [JsonPropertyName("stopOnActionError")]
    public bool StopOnActionError { get; init; } = true;
}

public sealed record MicroflowLoopExecutionContext
{
    public RuntimeExecutionContext RuntimeExecutionContext { get; init; } = null!;
    public MicroflowExecutionPlan ExecutionPlan { get; init; } = new();
    public MicroflowExecutionNode LoopNode { get; init; } = new();
    public MicroflowExecutionLoopCollection LoopCollection { get; init; } = new();
    public IMicroflowVariableStore VariableStore { get; init; } = null!;
    public IMicroflowExpressionEvaluator ExpressionEvaluator { get; init; } = null!;
    public IMicroflowActionExecutorRegistry ActionExecutorRegistry { get; init; } = null!;
    public IMicroflowTransactionManager? TransactionManager { get; init; }
    public MicroflowLoopExecutionOptions Options { get; init; } = new();
    public int CurrentIterationIndex { get; init; }
    public IReadOnlyList<MicroflowVariableScopeFrame> LoopStack { get; init; } = Array.Empty<MicroflowVariableScopeFrame>();
    public IReadOnlyList<MicroflowExecutionDiagnosticDto> Diagnostics { get; init; } = Array.Empty<MicroflowExecutionDiagnosticDto>();
    public CancellationToken CancellationToken { get; init; }
}

public sealed record MicroflowLoopIterationContext
{
    [JsonPropertyName("loopObjectId")]
    public string LoopObjectId { get; init; } = string.Empty;

    [JsonPropertyName("collectionId")]
    public string CollectionId { get; init; } = string.Empty;

    [JsonPropertyName("index")]
    public int Index { get; init; }

    [JsonPropertyName("iteratorVariableName")]
    public string? IteratorVariableName { get; init; }

    [JsonPropertyName("iteratorValuePreview")]
    public string? IteratorValuePreview { get; init; }

    [JsonPropertyName("parentLoopObjectId")]
    public string? ParentLoopObjectId { get; init; }

    [JsonPropertyName("depth")]
    public int Depth { get; init; }

    [JsonPropertyName("controlSignal")]
    public string ControlSignal { get; init; } = MicroflowLoopControlSignal.None;

    [JsonPropertyName("conditionResult")]
    public bool? ConditionResult { get; init; }

    [JsonPropertyName("itemCount")]
    public int? ItemCount { get; init; }

    [JsonIgnore]
    public JsonElement? IteratorRawValue { get; init; }

    [JsonIgnore]
    public string? IteratorDataTypeJson { get; init; }

    [JsonIgnore]
    public JsonElement LoopIterationJson { get; init; }

    public MicroflowLoopIterationContext WithControlSignal(string controlSignal)
        => this with
        {
            ControlSignal = controlSignal,
            LoopIterationJson = MicroflowLoopIterationJson.Create(this with { ControlSignal = controlSignal })
        };
}

public sealed record MicroflowLoopBodyExecutionResult
{
    public string Status { get; init; } = MicroflowLoopBodyExecutionStatus.Success;
    public MicroflowRuntimeErrorDto? Error { get; init; }
    public IReadOnlyList<MicroflowTraceFrameDto> TraceFrames { get; init; } = Array.Empty<MicroflowTraceFrameDto>();
    public JsonElement? Output { get; init; }
}

public sealed record MicroflowLoopExecutionResult
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = MicroflowLoopExecutionStatus.Success;

    [JsonPropertyName("iterationCount")]
    public int IterationCount { get; init; }

    [JsonPropertyName("traceFrames")]
    public IReadOnlyList<MicroflowTraceFrameDto> TraceFrames { get; init; } = Array.Empty<MicroflowTraceFrameDto>();

    [JsonPropertyName("logs")]
    public IReadOnlyList<MicroflowRuntimeLogDto> Logs { get; init; } = Array.Empty<MicroflowRuntimeLogDto>();

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<MicroflowExecutionDiagnosticDto> Diagnostics { get; init; } = Array.Empty<MicroflowExecutionDiagnosticDto>();

    [JsonPropertyName("error")]
    public MicroflowRuntimeErrorDto? Error { get; init; }

    [JsonPropertyName("finalOutgoingFlowId")]
    public string? FinalOutgoingFlowId { get; init; }

    [JsonPropertyName("transactionSnapshot")]
    public MicroflowRuntimeTransactionSnapshot? TransactionSnapshot { get; init; }

    [JsonPropertyName("outputPreview")]
    public JsonElement? OutputPreview { get; init; }
}

public static class MicroflowLoopIterationJson
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static JsonElement Create(MicroflowLoopIterationContext context)
        => JsonSerializer.SerializeToElement(new
        {
            loopObjectId = context.LoopObjectId,
            collectionId = context.CollectionId,
            index = context.Index,
            iteratorVariableName = context.IteratorVariableName,
            iteratorValuePreview = context.IteratorValuePreview,
            parentLoopObjectId = context.ParentLoopObjectId,
            depth = context.Depth,
            controlSignal = context.ControlSignal,
            conditionResult = context.ConditionResult,
            itemCount = context.ItemCount
        }, JsonOptions);
}
