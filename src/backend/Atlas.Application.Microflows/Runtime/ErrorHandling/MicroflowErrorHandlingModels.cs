using System.Text.Json;
using System.Text.Json.Serialization;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime.Actions;
using Atlas.Application.Microflows.Runtime.Transactions;

namespace Atlas.Application.Microflows.Runtime.ErrorHandling;

public static class MicroflowErrorHandlingType
{
    public const string Rollback = "rollback";
    public const string CustomWithRollback = "customWithRollback";
    public const string CustomWithoutRollback = "customWithoutRollback";
    public const string Continue = "continue";
}

public static class MicroflowErrorHandlingStatus
{
    public const string Handled = "handled";
    public const string Failed = "failed";
    public const string Continued = "continued";
    public const string RolledBack = "rolledBack";
    public const string EnteredErrorHandler = "enteredErrorHandler";
    public const string Unhandled = "unhandled";
}

public sealed record MicroflowErrorHandlingContext
{
    public RuntimeExecutionContext RuntimeContext { get; init; } = null!;
    public MicroflowExecutionPlan Plan { get; init; } = new();
    public MicroflowExecutionNode SourceNode { get; init; } = new();
    public MicroflowActionExecutionResult ActionResult { get; init; } = new();
    public MicroflowRuntimeErrorDto Error { get; init; } = new();
    public MicroflowExecutionFlow? ErrorHandlerFlow { get; init; }
    public string ErrorHandlingType { get; init; } = MicroflowErrorHandlingType.Rollback;
    public string SourceObjectId { get; init; } = string.Empty;
    public string? SourceActionId { get; init; }
    public string? CollectionId { get; init; }
    public string? IncomingFlowId { get; init; }
    public string? NormalOutgoingFlowId { get; init; }
    public JsonElement? LatestHttpResponse { get; init; }
    public JsonElement? LatestSoapFault { get; init; }
    public int ErrorDepth { get; init; }
    public bool IsInsideErrorHandler { get; init; }
    public JsonElement? LoopIteration { get; init; }
    public CancellationToken CancellationToken { get; init; }
}

public sealed record MicroflowErrorHandlingResult
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = MicroflowErrorHandlingStatus.Unhandled;

    [JsonPropertyName("nextFlowId")]
    public string? NextFlowId { get; init; }

    [JsonPropertyName("nextObjectId")]
    public string? NextObjectId { get; init; }

    [JsonPropertyName("traceFrames")]
    public IReadOnlyList<MicroflowTraceFrameDto> TraceFrames { get; init; } = Array.Empty<MicroflowTraceFrameDto>();

    [JsonPropertyName("logs")]
    public IReadOnlyList<MicroflowRuntimeLogDto> Logs { get; init; } = Array.Empty<MicroflowRuntimeLogDto>();

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<MicroflowErrorHandlingDiagnostic> Diagnostics { get; init; } = Array.Empty<MicroflowErrorHandlingDiagnostic>();

    [JsonPropertyName("transactionSnapshot")]
    public MicroflowRuntimeTransactionSnapshot? TransactionSnapshot { get; init; }

    [JsonPropertyName("latestErrorWritten")]
    public bool LatestErrorWritten { get; init; }

    [JsonPropertyName("latestHttpResponseWritten")]
    public bool LatestHttpResponseWritten { get; init; }

    [JsonPropertyName("latestSoapFaultWritten")]
    public bool LatestSoapFaultWritten { get; init; }

    [JsonPropertyName("error")]
    public MicroflowRuntimeErrorDto? Error { get; init; }

    [JsonPropertyName("shouldStopRun")]
    public bool ShouldStopRun { get; init; }

    [JsonPropertyName("shouldContinueNormalFlow")]
    public bool ShouldContinueNormalFlow { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("output")]
    public JsonElement Output { get; init; }
}

public sealed record MicroflowRuntimeErrorContext
{
    [JsonPropertyName("errorId")]
    public string ErrorId { get; init; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("code")]
    public string Code { get; init; } = RuntimeErrorCode.RuntimeUnknownError;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("sourceObjectId")]
    public string? SourceObjectId { get; init; }

    [JsonPropertyName("sourceActionId")]
    public string? SourceActionId { get; init; }

    [JsonPropertyName("sourceFlowId")]
    public string? SourceFlowId { get; init; }

    [JsonPropertyName("collectionId")]
    public string? CollectionId { get; init; }

    [JsonPropertyName("actionKind")]
    public string? ActionKind { get; init; }

    [JsonPropertyName("cause")]
    public string? Cause { get; init; }

    [JsonPropertyName("latestHttpResponse")]
    public JsonElement? LatestHttpResponse { get; init; }

    [JsonPropertyName("latestSoapFault")]
    public JsonElement? LatestSoapFault { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    [JsonPropertyName("callStackFrameId")]
    public string? CallStackFrameId { get; init; }

    [JsonPropertyName("loopIteration")]
    public JsonElement? LoopIteration { get; init; }

    [JsonPropertyName("transactionStatus")]
    public string? TransactionStatus { get; init; }
}

public sealed record MicroflowLatestErrorValue
{
    public MicroflowRuntimeErrorContext Error { get; init; } = new();
}

public sealed record MicroflowLatestHttpResponseValue
{
    public JsonElement Response { get; init; }
}

public sealed record MicroflowLatestSoapFaultValue
{
    public JsonElement Fault { get; init; }
}

public sealed record MicroflowErrorHandlingDiagnostic
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; init; } = "info";

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("objectId")]
    public string? ObjectId { get; init; }

    [JsonPropertyName("actionId")]
    public string? ActionId { get; init; }

    [JsonPropertyName("flowId")]
    public string? FlowId { get; init; }
}

public sealed record MicroflowErrorHandlerFlowResolution
{
    public MicroflowExecutionFlow? Flow { get; init; }
    public IReadOnlyList<MicroflowErrorHandlingDiagnostic> Diagnostics { get; init; } = Array.Empty<MicroflowErrorHandlingDiagnostic>();
}

public sealed record MicroflowErrorHandlingSummary
{
    [JsonPropertyName("handledErrorCount")]
    public int HandledErrorCount { get; init; }

    [JsonPropertyName("unhandledErrorCount")]
    public int UnhandledErrorCount { get; init; }

    [JsonPropertyName("continuedErrorCount")]
    public int ContinuedErrorCount { get; init; }

    [JsonPropertyName("rollbackCount")]
    public int RollbackCount { get; init; }

    [JsonPropertyName("customHandlerCount")]
    public int CustomHandlerCount { get; init; }

    [JsonPropertyName("errorEventCount")]
    public int ErrorEventCount { get; init; }

    [JsonPropertyName("latestErrorPreview")]
    public string? LatestErrorPreview { get; init; }
}
