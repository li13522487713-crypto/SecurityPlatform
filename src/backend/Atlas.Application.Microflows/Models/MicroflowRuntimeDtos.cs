using System.Text.Json;
using System.Text.Json.Serialization;
using Atlas.Application.Microflows.Runtime.Transactions;

namespace Atlas.Application.Microflows.Models;

public record TestRunMicroflowApiRequest
{
    [JsonPropertyName("schema")]
    public JsonElement? Schema { get; init; }

    [JsonPropertyName("input")]
    public IReadOnlyDictionary<string, JsonElement>? Input { get; init; }

    [JsonPropertyName("options")]
    public MicroflowTestRunOptionsDto? Options { get; init; }
}

public sealed record TestRunMicroflowRequestDto : TestRunMicroflowApiRequest;

public sealed record MicroflowTestRunOptionsDto
{
    [JsonPropertyName("simulateRestError")]
    public bool? SimulateRestError { get; init; }

    [JsonPropertyName("allowRealHttp")]
    public bool? AllowRealHttp { get; init; }

    [JsonPropertyName("decisionBooleanResult")]
    public bool? DecisionBooleanResult { get; init; }

    [JsonPropertyName("enumerationCaseValue")]
    public string? EnumerationCaseValue { get; init; }

    [JsonPropertyName("objectTypeCase")]
    public string? ObjectTypeCase { get; init; }

    [JsonPropertyName("loopIterations")]
    public int? LoopIterations { get; init; }

    [JsonPropertyName("maxSteps")]
    public int? MaxSteps { get; init; }

    [JsonPropertyName("disableExpressionEvaluation")]
    public bool DisableExpressionEvaluation { get; init; }
}

public sealed record TestRunMicroflowApiResponse
{
    [JsonPropertyName("session")]
    public MicroflowRunSessionDto Session { get; init; } = new();
}

public record CancelMicroflowRunResponse
{
    [JsonPropertyName("runId")]
    public string RunId { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = "cancelled";
}

public sealed record CancelMicroflowRunResponseDto : CancelMicroflowRunResponse;

public sealed record MicroflowRunSessionDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("schemaId")]
    public string SchemaId { get; init; } = string.Empty;

    [JsonPropertyName("resourceId")]
    public string ResourceId { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("parentRunId")]
    public string? ParentRunId { get; init; }

    [JsonPropertyName("rootRunId")]
    public string? RootRunId { get; init; }

    [JsonPropertyName("callFrameId")]
    public string? CallFrameId { get; init; }

    [JsonPropertyName("callDepth")]
    public int? CallDepth { get; init; }

    [JsonPropertyName("startedAt")]
    public DateTimeOffset StartedAt { get; init; }

    [JsonPropertyName("endedAt")]
    public DateTimeOffset? EndedAt { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = "failed";

    [JsonPropertyName("input")]
    public IReadOnlyDictionary<string, JsonElement> Input { get; init; } = new Dictionary<string, JsonElement>();

    [JsonPropertyName("output")]
    public JsonElement? Output { get; init; }

    [JsonPropertyName("error")]
    public MicroflowRuntimeErrorDto? Error { get; init; }

    [JsonPropertyName("trace")]
    public IReadOnlyList<MicroflowTraceFrameDto> Trace { get; init; } = Array.Empty<MicroflowTraceFrameDto>();

    [JsonPropertyName("logs")]
    public IReadOnlyList<MicroflowRuntimeLogDto> Logs { get; init; } = Array.Empty<MicroflowRuntimeLogDto>();

    [JsonPropertyName("variables")]
    public IReadOnlyList<MicroflowVariableSnapshotDto> Variables { get; init; } = Array.Empty<MicroflowVariableSnapshotDto>();

    [JsonPropertyName("transactionSummary")]
    public MicroflowRuntimeTransactionSummary? TransactionSummary { get; init; }

    [JsonPropertyName("childRuns")]
    public IReadOnlyList<MicroflowRunSessionDto> ChildRuns { get; init; } = Array.Empty<MicroflowRunSessionDto>();

    [JsonPropertyName("childRunIds")]
    public IReadOnlyList<string> ChildRunIds { get; init; } = Array.Empty<string>();
}

public sealed record MicroflowTraceFrameDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("runId")]
    public string RunId { get; init; } = string.Empty;

    [JsonPropertyName("parentRunId")]
    public string? ParentRunId { get; init; }

    [JsonPropertyName("rootRunId")]
    public string? RootRunId { get; init; }

    [JsonPropertyName("callFrameId")]
    public string? CallFrameId { get; init; }

    [JsonPropertyName("callDepth")]
    public int? CallDepth { get; init; }

    [JsonPropertyName("callerObjectId")]
    public string? CallerObjectId { get; init; }

    [JsonPropertyName("callerActionId")]
    public string? CallerActionId { get; init; }

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
    public string Status { get; init; } = "skipped";

    [JsonPropertyName("startedAt")]
    public DateTimeOffset StartedAt { get; init; }

    [JsonPropertyName("endedAt")]
    public DateTimeOffset? EndedAt { get; init; }

    [JsonPropertyName("durationMs")]
    public int DurationMs { get; init; }

    [JsonPropertyName("input")]
    public JsonElement? Input { get; init; }

    [JsonPropertyName("output")]
    public JsonElement? Output { get; init; }

    [JsonPropertyName("error")]
    public MicroflowRuntimeErrorDto? Error { get; init; }

    [JsonPropertyName("variablesSnapshot")]
    public IReadOnlyDictionary<string, MicroflowRuntimeVariableValueDto>? VariablesSnapshot { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("errorHandlerVisited")]
    public bool? ErrorHandlerVisited { get; init; }
}

public sealed record MicroflowRuntimeErrorDto
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

public sealed record MicroflowRuntimeLogDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    [JsonPropertyName("level")]
    public string Level { get; init; } = "info";

    [JsonPropertyName("objectId")]
    public string? ObjectId { get; init; }

    [JsonPropertyName("actionId")]
    public string? ActionId { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("logNodeName")]
    public string? LogNodeName { get; init; }

    [JsonPropertyName("traceId")]
    public string? TraceId { get; init; }

    [JsonPropertyName("variablesPreview")]
    public JsonElement? VariablesPreview { get; init; }

    [JsonPropertyName("structuredFieldsJson")]
    public string? StructuredFieldsJson { get; init; }
}

public record MicroflowVariableSnapshotDto
{
    [JsonPropertyName("frameId")]
    public string? FrameId { get; init; }

    [JsonPropertyName("objectId")]
    public string ObjectId { get; init; } = string.Empty;

    [JsonPropertyName("variables")]
    public IReadOnlyList<MicroflowRuntimeVariableValueDto> Variables { get; init; } = Array.Empty<MicroflowRuntimeVariableValueDto>();
}

public sealed record MicroflowRunSessionVariableSnapshotDto : MicroflowVariableSnapshotDto;

public sealed record MicroflowRuntimeVariableValueDto
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public JsonElement? Type { get; init; }

    [JsonPropertyName("valuePreview")]
    public string ValuePreview { get; init; } = string.Empty;

    [JsonPropertyName("rawValue")]
    public JsonElement? RawValue { get; init; }

    [JsonPropertyName("rawValueJson")]
    public string? RawValueJson { get; init; }

    [JsonPropertyName("source")]
    public string? Source { get; init; }

    [JsonPropertyName("readonly")]
    public bool? Readonly { get; init; }

    [JsonPropertyName("scopeKind")]
    public string? ScopeKind { get; init; }
}

public record GetMicroflowRunTraceResponse
{
    [JsonPropertyName("runId")]
    public string RunId { get; init; } = string.Empty;

    [JsonPropertyName("trace")]
    public IReadOnlyList<MicroflowTraceFrameDto> Trace { get; init; } = Array.Empty<MicroflowTraceFrameDto>();

    [JsonPropertyName("logs")]
    public IReadOnlyList<MicroflowRuntimeLogDto> Logs { get; init; } = Array.Empty<MicroflowRuntimeLogDto>();
}

public sealed record MicroflowRunTraceResponseDto : GetMicroflowRunTraceResponse;

public static class RuntimeErrorCode
{
    public const string RuntimeStartNotFound = "RUNTIME_START_NOT_FOUND";
    public const string RuntimeEndNotReached = "RUNTIME_END_NOT_REACHED";
    public const string RuntimeFlowNotFound = "RUNTIME_FLOW_NOT_FOUND";
    public const string RuntimeInvalidCase = "RUNTIME_INVALID_CASE";
    public const string RuntimeVariableNotFound = "RUNTIME_VARIABLE_NOT_FOUND";
    public const string RuntimeVariableTypeMismatch = "RUNTIME_VARIABLE_TYPE_MISMATCH";
    public const string RuntimeVariableReadonly = "RUNTIME_VARIABLE_READONLY";
    public const string RuntimeVariableDuplicated = "RUNTIME_VARIABLE_DUPLICATED";
    public const string RuntimeVariableScopeError = "RUNTIME_VARIABLE_SCOPE_ERROR";
    public const string RuntimeExpressionError = "RUNTIME_EXPRESSION_ERROR";
    public const string RuntimeMetadataNotFound = "RUNTIME_METADATA_NOT_FOUND";
    public const string RuntimeEntityAccessDenied = "RUNTIME_ENTITY_ACCESS_DENIED";
    public const string RuntimeObjectNotFound = "RUNTIME_OBJECT_NOT_FOUND";
    public const string RuntimeRetrieveFailed = "RUNTIME_RETRIEVE_FAILED";
    public const string RuntimeCommitFailed = "RUNTIME_COMMIT_FAILED";
    public const string RuntimeDeleteFailed = "RUNTIME_DELETE_FAILED";
    public const string RuntimeRestCallFailed = "RUNTIME_REST_CALL_FAILED";
    public const string RuntimeRestInvalidUrl = "RUNTIME_REST_INVALID_URL";
    public const string RuntimeRestUrlBlocked = "RUNTIME_REST_URL_BLOCKED";
    public const string RuntimeRestPrivateNetworkBlocked = "RUNTIME_REST_PRIVATE_NETWORK_BLOCKED";
    public const string RuntimeRestDeniedHost = "RUNTIME_REST_DENIED_HOST";
    public const string RuntimeRestUnsupportedScheme = "RUNTIME_REST_UNSUPPORTED_SCHEME";
    public const string RuntimeRestTimeout = "RUNTIME_REST_TIMEOUT";
    public const string RuntimeRestResponseTooLarge = "RUNTIME_REST_RESPONSE_TOO_LARGE";
    public const string RuntimeRestResponseParseFailed = "RUNTIME_REST_RESPONSE_PARSE_FAILED";
    public const string RuntimeTimeout = "RUNTIME_TIMEOUT";
    public const string RuntimeCallMicroflowFailed = "RUNTIME_CALL_MICROFLOW_FAILED";
    public const string RuntimeCallRecursionDetected = "RUNTIME_CALL_RECURSION_DETECTED";
    public const string RuntimeCallStackOverflow = "RUNTIME_CALL_STACK_OVERFLOW";
    public const string RuntimeUnsupportedAction = "RUNTIME_UNSUPPORTED_ACTION";
    public const string RuntimeConnectorRequired = "RUNTIME_CONNECTOR_REQUIRED";
    public const string RuntimeLogMessageFailed = "RUNTIME_LOG_MESSAGE_FAILED";
    public const string RuntimeTransactionRolledBack = "RUNTIME_TRANSACTION_ROLLED_BACK";
    public const string RuntimeLoopSourceNotFound = "RUNTIME_LOOP_SOURCE_NOT_FOUND";
    public const string RuntimeLoopSourceNotList = "RUNTIME_LOOP_SOURCE_NOT_LIST";
    public const string RuntimeLoopIteratorInvalid = "RUNTIME_LOOP_ITERATOR_INVALID";
    public const string RuntimeLoopConditionError = "RUNTIME_LOOP_CONDITION_ERROR";
    public const string RuntimeLoopConditionNotBoolean = "RUNTIME_LOOP_CONDITION_NOT_BOOLEAN";
    public const string RuntimeLoopMaxIterationsExceeded = "RUNTIME_LOOP_MAX_ITERATIONS_EXCEEDED";
    public const string RuntimeLoopBodyNotFound = "RUNTIME_LOOP_BODY_NOT_FOUND";
    public const string RuntimeLoopControlOutOfScope = "RUNTIME_LOOP_CONTROL_OUT_OF_SCOPE";
    public const string RuntimeLoopDeadEnd = "RUNTIME_LOOP_DEAD_END";
    public const string RuntimeMaxStepsExceeded = "RUNTIME_MAX_STEPS_EXCEEDED";
    public const string RuntimeCancelled = "RUNTIME_CANCELLED";
    public const string RuntimeErrorEventReached = "RUNTIME_ERROR_EVENT_REACHED";
    public const string RuntimeUnknownError = "RUNTIME_UNKNOWN_ERROR";
    public const string RuntimeExpressionParseError = "RUNTIME_EXPR_PARSE_ERROR";
    public const string RuntimeExpressionMemberNotFound = "RUNTIME_EXPR_MEMBER_NOT_FOUND";
    public const string RuntimeExpressionUnsupportedFunction = "RUNTIME_EXPR_UNSUPPORTED_FUNCTION";
    public const string RuntimeExpressionDivideByZero = "RUNTIME_EXPR_DIVIDE_BY_ZERO";
    public const string RuntimeExpressionExpectedTypeMismatch = "RUNTIME_EXPR_EXPECTED_TYPE_MISMATCH";
}
