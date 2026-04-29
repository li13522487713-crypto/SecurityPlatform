using System.Text.Json;
using System.Text.Json.Serialization;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime.Expressions;
using Atlas.Application.Microflows.Runtime.Loops;
using Atlas.Application.Microflows.Runtime.Metadata;
using Atlas.Application.Microflows.Runtime.Security;
using Atlas.Application.Microflows.Runtime.Transactions;
using Microsoft.Extensions.Logging;

namespace Atlas.Application.Microflows.Runtime.Actions;

public static class MicroflowActionRuntimeCategory
{
    public const string ServerExecutable = "serverExecutable";
    public const string RuntimeCommand = "runtimeCommand";
    public const string ConnectorBacked = "connectorBacked";
    public const string ExplicitUnsupported = "explicitUnsupported";
}

public static class MicroflowActionSupportLevel
{
    public const string Supported = "supported";
    public const string ModeledOnlyConverted = "modeledOnlyConverted";
    public const string RequiresConnector = "requiresConnector";
    public const string Unsupported = "unsupported";
    public const string NanoflowOnly = "nanoflowOnly";
    public const string Deprecated = "deprecated";
    public const string Unsafe = "unsafe";
}

public static class MicroflowRuntimeConnectorCapability
{
    public const string ObjectStoreCrud = "objectStore.crud";
    public const string RestRealHttp = "rest.realHttp";
    public const string RestExportMapping = "rest.exportMapping";
    public const string RestImportMapping = "rest.importMapping";
    public const string SoapWebService = "soap.webService";
    public const string XmlImportMapping = "xml.importMapping";
    public const string XmlExportMapping = "xml.exportMapping";
    public const string DocumentGeneration = "document.generation";
    public const string ExternalObjectCrud = "externalObject.crud";
    public const string WorkflowAction = "workflow.action";
    public const string JavaAction = "java.action";
    public const string MlModel = "ml.model";
    public const string MetricsEmit = "metrics.emit";
    public const string EmailSend = "email.send";
    public const string NotificationSend = "notification.send";
    public const string MessagePublish = "message.publish";
    public const string MessageReceive = "message.receive";
    public const string ODataAction = "odata.action";
    public const string ODataRetrieve = "odata.retrieve";
    public const string ODataCommit = "odata.commit";
    public const string ODataDelete = "odata.delete";
    public const string FileDocumentRead = "fileDocument.read";
    public const string FileDocumentWrite = "fileDocument.write";
    public const string ClientCommand = "client.command";
}

public static class MicroflowActionExecutionStatus
{
    public const string Success = "success";
    public const string Failed = "failed";
    public const string PendingClientCommand = "pendingClientCommand";
    public const string ConnectorRequired = "connectorRequired";
    public const string Unsupported = "unsupported";
}

public interface IMicroflowActionExecutor
{
    string ActionKind { get; }

    string Category { get; }

    string SupportLevel { get; }

    Task<MicroflowActionExecutionResult> ExecuteAsync(
        MicroflowActionExecutionContext context,
        CancellationToken ct);
}

public interface IMicroflowActionExecutorRegistry
{
    void Register(IMicroflowActionExecutor executor);

    bool TryGet(string? actionKind, out IMicroflowActionExecutor executor);

    IMicroflowActionExecutor GetOrFallback(string? actionKind);

    string GetSupportLevel(string? actionKind);

    string GetCategory(string? actionKind);

    IReadOnlyList<MicroflowActionExecutorDescriptor> ListAll();

    MicroflowActionExecutorCoverageDiagnostic ValidateCoverage(IEnumerable<string> actionKinds);

    void EnsureEveryActionKindCovered(IEnumerable<string> actionKinds);
}

public interface IMicroflowRuntimeConnectorRegistry
{
    bool HasCapability(string capability);

    IReadOnlyList<string> ListEnabledCapabilities();

    Task<MicroflowConnectorExecutionResult> ExecuteAsync(
        MicroflowConnectorExecutionRequest request,
        CancellationToken ct);
}

public interface IMicroflowRuntimeConnector
{
    string Capability { get; }

    bool Enabled { get; }

    Task<MicroflowConnectorExecutionResult> ExecuteAsync(
        MicroflowConnectorExecutionRequest request,
        CancellationToken ct);
}

public sealed record MicroflowActionExecutorDescriptor
{
    [JsonPropertyName("actionKind")]
    public string ActionKind { get; init; } = string.Empty;

    [JsonPropertyName("schemaType")]
    public string SchemaType { get; init; } = "MicroflowGenericAction";

    [JsonPropertyName("registryCategory")]
    public string RegistryCategory { get; init; } = string.Empty;

    [JsonPropertyName("runtimeCategory")]
    public string RuntimeCategory { get; init; } = MicroflowActionRuntimeCategory.ExplicitUnsupported;

    [JsonPropertyName("supportLevel")]
    public string SupportLevel { get; init; } = MicroflowActionSupportLevel.Unsupported;

    [JsonPropertyName("executor")]
    public string Executor { get; init; } = string.Empty;

    [JsonPropertyName("connectorCapability")]
    public string? ConnectorCapability { get; init; }

    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; init; }

    [JsonPropertyName("reason")]
    public string Reason { get; init; } = string.Empty;

    [JsonPropertyName("realExecution")]
    public bool RealExecution { get; init; }

    [JsonPropertyName("producesVariables")]
    public bool ProducesVariables { get; init; }

    [JsonPropertyName("producesTransaction")]
    public bool ProducesTransaction { get; init; }

    [JsonPropertyName("producesRuntimeCommand")]
    public bool ProducesRuntimeCommand { get; init; }

    [JsonPropertyName("verifyCovered")]
    public bool VerifyCovered { get; init; }
}

public sealed record MicroflowActionExecutionContext
{
    public RuntimeExecutionContext RuntimeExecutionContext { get; init; } = null!;
    public MicroflowExecutionPlan ExecutionPlan { get; init; } = new();
    public MicroflowExecutionNode ExecutionNode { get; init; } = new();
    public JsonElement ActionConfig { get; init; }
    public string ActionKind { get; init; } = string.Empty;
    public string ObjectId { get; init; } = string.Empty;
    public string? ActionId { get; init; }
    public string? CollectionId { get; init; }
    public IMicroflowVariableStore VariableStore { get; init; } = null!;
    public IMicroflowExpressionEvaluator? ExpressionEvaluator { get; init; }
    public IMicroflowMetadataResolver? MetadataResolver { get; init; }
    public MicroflowMetadataCatalogDto? MetadataCatalog { get; init; }
    public IMicroflowEntityAccessService? EntityAccessService { get; init; }
    public IMicroflowTransactionManager? TransactionManager { get; init; }
    public IMicroflowRuntimeConnectorRegistry ConnectorRegistry { get; init; } = null!;
    public MicroflowRuntimeSecurityContext RuntimeSecurityContext { get; init; } = MicroflowRuntimeSecurityContext.System();
    public MicroflowActionExecutionOptions Options { get; init; } = new();
    public MicroflowLoopExecutionOptions? LoopExecutionOptions { get; init; }
    public Func<MicroflowLoopIterationContext, CancellationToken, Task<MicroflowLoopBodyExecutionResult>>? LoopBodyExecutor { get; init; }
    public ILogger? Logger { get; init; }
}

public sealed record MicroflowActionExecutionOptions
{
    public string Mode { get; init; } = MicroflowRuntimeExecutionMode.TestRun;
    public bool AllowRealHttp { get; init; }
    public bool SimulateRestError { get; init; }
    public bool StopOnUnsupported { get; init; } = true;
    public bool StrictMetrics { get; init; }
    public int MaxCallDepth { get; init; } = 10;
    public IReadOnlyList<string> ConnectorCapabilities { get; init; } = Array.Empty<string>();
}

public sealed record MicroflowActionExecutionResult
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = MicroflowActionExecutionStatus.Success;

    [JsonPropertyName("outputJson")]
    public JsonElement? OutputJson { get; init; }

    [JsonPropertyName("outputPreview")]
    public string? OutputPreview { get; init; }

    [JsonPropertyName("producedVariables")]
    public IReadOnlyList<MicroflowRuntimeVariableValueDto> ProducedVariables { get; init; } = Array.Empty<MicroflowRuntimeVariableValueDto>();

    [JsonPropertyName("runtimeCommands")]
    public IReadOnlyList<MicroflowRuntimeCommand> RuntimeCommands { get; init; } = Array.Empty<MicroflowRuntimeCommand>();

    [JsonPropertyName("connectorRequests")]
    public IReadOnlyList<MicroflowConnectorExecutionRequest> ConnectorRequests { get; init; } = Array.Empty<MicroflowConnectorExecutionRequest>();

    [JsonPropertyName("transactionSnapshot")]
    public MicroflowRuntimeTransactionSnapshot? TransactionSnapshot { get; init; }

    [JsonPropertyName("logs")]
    public IReadOnlyList<MicroflowRuntimeLogDto> Logs { get; init; } = Array.Empty<MicroflowRuntimeLogDto>();

    [JsonPropertyName("childRunSessions")]
    public IReadOnlyList<MicroflowRunSessionDto> ChildRunSessions { get; init; } = Array.Empty<MicroflowRunSessionDto>();

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<MicroflowActionExecutionDiagnostic> Diagnostics { get; init; } = Array.Empty<MicroflowActionExecutionDiagnostic>();

    [JsonPropertyName("error")]
    public MicroflowRuntimeErrorDto? Error { get; init; }

    [JsonPropertyName("latestHttpResponse")]
    public JsonElement? LatestHttpResponse { get; init; }

    [JsonPropertyName("durationMs")]
    public int DurationMs { get; init; }

    [JsonPropertyName("shouldContinueNormalFlow")]
    public bool ShouldContinueNormalFlow { get; init; } = true;

    [JsonPropertyName("shouldEnterErrorHandler")]
    public bool ShouldEnterErrorHandler { get; init; }

    [JsonPropertyName("shouldStopRun")]
    public bool ShouldStopRun { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }
}

public sealed record MicroflowRuntimeCommand
{
    [JsonPropertyName("commandId")]
    public string CommandId { get; init; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("commandKind")]
    public string CommandKind { get; init; } = string.Empty;

    [JsonPropertyName("sourceObjectId")]
    public string? SourceObjectId { get; init; }

    [JsonPropertyName("sourceActionId")]
    public string? SourceActionId { get; init; }

    [JsonPropertyName("payloadJson")]
    public string? PayloadJson { get; init; }

    [JsonPropertyName("requiresClientHandling")]
    public bool RequiresClientHandling { get; init; } = true;

    [JsonPropertyName("status")]
    public string Status { get; init; } = "pending";

    [JsonPropertyName("message")]
    public string? Message { get; init; }
}

public sealed record MicroflowConnectorExecutionRequest
{
    [JsonPropertyName("requestId")]
    public string RequestId { get; init; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("capability")]
    public string Capability { get; init; } = string.Empty;

    [JsonPropertyName("actionKind")]
    public string ActionKind { get; init; } = string.Empty;

    [JsonPropertyName("objectId")]
    public string? ObjectId { get; init; }

    [JsonPropertyName("actionId")]
    public string? ActionId { get; init; }

    [JsonPropertyName("payloadJson")]
    public string? PayloadJson { get; init; }
}

public sealed record MicroflowConnectorExecutionResult
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("capability")]
    public string Capability { get; init; } = string.Empty;

    [JsonPropertyName("outputJson")]
    public string? OutputJson { get; init; }

    [JsonPropertyName("logs")]
    public IReadOnlyList<MicroflowRuntimeLogDto> Logs { get; init; } = Array.Empty<MicroflowRuntimeLogDto>();

    [JsonPropertyName("error")]
    public MicroflowRuntimeErrorDto? Error { get; init; }
}

public sealed record MicroflowActionExecutionDiagnostic
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; init; } = "info";

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("actionKind")]
    public string? ActionKind { get; init; }

    [JsonPropertyName("objectId")]
    public string? ObjectId { get; init; }

    [JsonPropertyName("actionId")]
    public string? ActionId { get; init; }

    [JsonPropertyName("connectorCapability")]
    public string? ConnectorCapability { get; init; }
}

public sealed record MicroflowActionExecutorCoverageDiagnostic
{
    [JsonPropertyName("covered")]
    public bool Covered { get; init; }

    [JsonPropertyName("missingActionKinds")]
    public IReadOnlyList<string> MissingActionKinds { get; init; } = Array.Empty<string>();

    [JsonPropertyName("coveredCount")]
    public int CoveredCount { get; init; }

    [JsonPropertyName("expectedCount")]
    public int ExpectedCount { get; init; }
}
