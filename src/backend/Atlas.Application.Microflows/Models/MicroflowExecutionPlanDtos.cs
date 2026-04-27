using System.Text.Json;
using System.Text.Json.Serialization;

namespace Atlas.Application.Microflows.Models;

public static class MicroflowExecutionPlanMode
{
    public const string TestRun = "testRun";
    public const string PublishedRun = "publishedRun";
    public const string PreviewRun = "previewRun";
    public const string ValidateOnly = "validateOnly";
}

public static class MicroflowRuntimeSupportLevel
{
    public const string Supported = "supported";
    public const string ModeledOnly = "modeledOnly";
    public const string Unsupported = "unsupported";
    public const string RequiresConnector = "requiresConnector";
    public const string NanoflowOnly = "nanoflowOnly";
    public const string Deprecated = "deprecated";
}

public sealed record MicroflowExecutionPlanLoadOptions
{
    [JsonPropertyName("resourceId")]
    public string? ResourceId { get; init; }

    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("mode")]
    public string Mode { get; init; } = MicroflowExecutionPlanMode.ValidateOnly;

    [JsonPropertyName("includeDiagnostics")]
    public bool IncludeDiagnostics { get; init; } = true;

    [JsonPropertyName("failOnUnsupported")]
    public bool FailOnUnsupported { get; init; }

    [JsonPropertyName("connectorCapabilities")]
    public IReadOnlyList<string> ConnectorCapabilities { get; init; } = Array.Empty<string>();

    [JsonPropertyName("workspaceId")]
    public string? WorkspaceId { get; init; }

    [JsonPropertyName("tenantId")]
    public string? TenantId { get; init; }

    [JsonPropertyName("userId")]
    public string? UserId { get; init; }
}

public sealed record LoadMicroflowExecutionPlanRequestDto
{
    [JsonPropertyName("schema")]
    public JsonElement Schema { get; init; }

    [JsonPropertyName("options")]
    public MicroflowExecutionPlanLoadOptions? Options { get; init; }
}

public sealed record MicroflowExecutionDiagnosticDto
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; init; } = "info";

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("objectId")]
    public string? ObjectId { get; init; }

    [JsonPropertyName("flowId")]
    public string? FlowId { get; init; }

    [JsonPropertyName("actionId")]
    public string? ActionId { get; init; }

    [JsonPropertyName("collectionId")]
    public string? CollectionId { get; init; }

    [JsonPropertyName("fieldPath")]
    public string? FieldPath { get; init; }
}

public sealed record MicroflowExecutionPlanValidationResult
{
    [JsonPropertyName("valid")]
    public bool Valid { get; init; }

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<MicroflowExecutionDiagnosticDto> Diagnostics { get; init; } = Array.Empty<MicroflowExecutionDiagnosticDto>();

    [JsonPropertyName("errorCount")]
    public int ErrorCount { get; init; }

    [JsonPropertyName("warningCount")]
    public int WarningCount { get; init; }

    [JsonPropertyName("unsupportedActionCount")]
    public int UnsupportedActionCount { get; init; }
}

public sealed record MicroflowExecutionPlan
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("schemaId")]
    public string SchemaId { get; init; } = string.Empty;

    [JsonPropertyName("resourceId")]
    public string? ResourceId { get; init; }

    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("schemaVersion")]
    public string? SchemaVersion { get; init; }

    [JsonPropertyName("startNodeId")]
    public string StartNodeId { get; init; } = string.Empty;

    [JsonPropertyName("endNodeIds")]
    public IReadOnlyList<string> EndNodeIds { get; init; } = Array.Empty<string>();

    [JsonPropertyName("parameters")]
    public IReadOnlyList<MicroflowExecutionParameter> Parameters { get; init; } = Array.Empty<MicroflowExecutionParameter>();

    [JsonPropertyName("nodes")]
    public IReadOnlyList<MicroflowExecutionNode> Nodes { get; init; } = Array.Empty<MicroflowExecutionNode>();

    [JsonPropertyName("flows")]
    public IReadOnlyList<MicroflowExecutionFlow> Flows { get; init; } = Array.Empty<MicroflowExecutionFlow>();

    [JsonPropertyName("normalFlows")]
    public IReadOnlyList<MicroflowExecutionFlow> NormalFlows { get; init; } = Array.Empty<MicroflowExecutionFlow>();

    [JsonPropertyName("decisionFlows")]
    public IReadOnlyList<MicroflowExecutionFlow> DecisionFlows { get; init; } = Array.Empty<MicroflowExecutionFlow>();

    [JsonPropertyName("objectTypeFlows")]
    public IReadOnlyList<MicroflowExecutionFlow> ObjectTypeFlows { get; init; } = Array.Empty<MicroflowExecutionFlow>();

    [JsonPropertyName("errorHandlerFlows")]
    public IReadOnlyList<MicroflowExecutionFlow> ErrorHandlerFlows { get; init; } = Array.Empty<MicroflowExecutionFlow>();

    [JsonPropertyName("ignoredFlows")]
    public IReadOnlyList<MicroflowExecutionFlow> IgnoredFlows { get; init; } = Array.Empty<MicroflowExecutionFlow>();

    [JsonPropertyName("loopCollections")]
    public IReadOnlyList<MicroflowExecutionLoopCollection> LoopCollections { get; init; } = Array.Empty<MicroflowExecutionLoopCollection>();

    [JsonPropertyName("variableDeclarations")]
    public IReadOnlyList<MicroflowExecutionVariableDeclaration> VariableDeclarations { get; init; } = Array.Empty<MicroflowExecutionVariableDeclaration>();

    [JsonPropertyName("metadataRefs")]
    public IReadOnlyList<MicroflowExecutionMetadataRef> MetadataRefs { get; init; } = Array.Empty<MicroflowExecutionMetadataRef>();

    [JsonPropertyName("unsupportedActions")]
    public IReadOnlyList<MicroflowUnsupportedActionDescriptor> UnsupportedActions { get; init; } = Array.Empty<MicroflowUnsupportedActionDescriptor>();

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<MicroflowExecutionDiagnosticDto> Diagnostics { get; init; } = Array.Empty<MicroflowExecutionDiagnosticDto>();

    [JsonPropertyName("validation")]
    public MicroflowExecutionPlanValidationResult Validation { get; init; } = new();

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed record MicroflowExecutionNode
{
    [JsonPropertyName("objectId")]
    public string ObjectId { get; init; } = string.Empty;

    [JsonPropertyName("actionId")]
    public string? ActionId { get; init; }

    [JsonPropertyName("collectionId")]
    public string? CollectionId { get; init; }

    [JsonPropertyName("parentLoopObjectId")]
    public string? ParentLoopObjectId { get; init; }

    [JsonPropertyName("kind")]
    public string Kind { get; init; } = string.Empty;

    [JsonPropertyName("officialType")]
    public string? OfficialType { get; init; }

    [JsonPropertyName("caption")]
    public string? Caption { get; init; }

    [JsonPropertyName("actionKind")]
    public string? ActionKind { get; init; }

    [JsonPropertyName("actionOfficialType")]
    public string? ActionOfficialType { get; init; }

    [JsonPropertyName("supportLevel")]
    public string SupportLevel { get; init; } = MicroflowRuntimeSupportLevel.Supported;

    [JsonPropertyName("runtimeBehavior")]
    public string RuntimeBehavior { get; init; } = "executable";

    [JsonPropertyName("configJson")]
    public JsonElement? ConfigJson { get; init; }

    [JsonPropertyName("errorHandling")]
    public MicroflowRuntimeErrorHandlingDto? ErrorHandling { get; init; }

    [JsonPropertyName("inputVariableNames")]
    public IReadOnlyList<string> InputVariableNames { get; init; } = Array.Empty<string>();

    [JsonPropertyName("outputVariableNames")]
    public IReadOnlyList<string> OutputVariableNames { get; init; } = Array.Empty<string>();

    [JsonPropertyName("metadataRefs")]
    public IReadOnlyList<MicroflowExecutionMetadataRef> MetadataRefs { get; init; } = Array.Empty<MicroflowExecutionMetadataRef>();

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<MicroflowExecutionDiagnosticDto> Diagnostics { get; init; } = Array.Empty<MicroflowExecutionDiagnosticDto>();
}

public sealed record MicroflowExecutionFlow
{
    [JsonPropertyName("flowId")]
    public string FlowId { get; init; } = string.Empty;

    [JsonPropertyName("collectionId")]
    public string? CollectionId { get; init; }

    [JsonPropertyName("edgeKind")]
    public string EdgeKind { get; init; } = "sequence";

    [JsonPropertyName("controlFlow")]
    public string ControlFlow { get; init; } = "normal";

    [JsonPropertyName("originObjectId")]
    public string? OriginObjectId { get; init; }

    [JsonPropertyName("destinationObjectId")]
    public string? DestinationObjectId { get; init; }

    [JsonPropertyName("originConnectionIndex")]
    public int? OriginConnectionIndex { get; init; }

    [JsonPropertyName("destinationConnectionIndex")]
    public int? DestinationConnectionIndex { get; init; }

    [JsonPropertyName("caseValues")]
    public IReadOnlyList<JsonElement> CaseValues { get; init; } = Array.Empty<JsonElement>();

    [JsonPropertyName("isErrorHandler")]
    public bool IsErrorHandler { get; init; }

    [JsonPropertyName("branchOrder")]
    public int? BranchOrder { get; init; }

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<MicroflowExecutionDiagnosticDto> Diagnostics { get; init; } = Array.Empty<MicroflowExecutionDiagnosticDto>();
}

public sealed record MicroflowExecutionLoopCollection
{
    [JsonPropertyName("loopObjectId")]
    public string LoopObjectId { get; init; } = string.Empty;

    [JsonPropertyName("collectionId")]
    public string CollectionId { get; init; } = string.Empty;

    [JsonPropertyName("parentCollectionId")]
    public string? ParentCollectionId { get; init; }

    [JsonPropertyName("nodes")]
    public IReadOnlyList<string> Nodes { get; init; } = Array.Empty<string>();

    [JsonPropertyName("flows")]
    public IReadOnlyList<string> Flows { get; init; } = Array.Empty<string>();

    [JsonPropertyName("startLikeNodeIds")]
    public IReadOnlyList<string> StartLikeNodeIds { get; init; } = Array.Empty<string>();

    [JsonPropertyName("terminalNodeIds")]
    public IReadOnlyList<string> TerminalNodeIds { get; init; } = Array.Empty<string>();
}

public sealed record MicroflowExecutionParameter
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("dataTypeJson")]
    public JsonElement DataTypeJson { get; init; }

    [JsonPropertyName("required")]
    public bool Required { get; init; }

    [JsonPropertyName("documentation")]
    public string? Documentation { get; init; }
}

public sealed record MicroflowExecutionVariableDeclaration
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("dataTypeJson")]
    public JsonElement DataTypeJson { get; init; }

    [JsonPropertyName("sourceKind")]
    public string SourceKind { get; init; } = string.Empty;

    [JsonPropertyName("sourceObjectId")]
    public string? SourceObjectId { get; init; }

    [JsonPropertyName("sourceActionId")]
    public string? SourceActionId { get; init; }

    [JsonPropertyName("collectionId")]
    public string? CollectionId { get; init; }

    [JsonPropertyName("loopObjectId")]
    public string? LoopObjectId { get; init; }

    [JsonPropertyName("readonly")]
    public bool Readonly { get; init; }

    [JsonPropertyName("scopeKind")]
    public string ScopeKind { get; init; } = "global";

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<MicroflowExecutionDiagnosticDto> Diagnostics { get; init; } = Array.Empty<MicroflowExecutionDiagnosticDto>();
}

public sealed record MicroflowRuntimeErrorHandlingDto
{
    [JsonPropertyName("errorHandlingType")]
    public string ErrorHandlingType { get; init; } = string.Empty;

    [JsonPropertyName("scopeObjectId")]
    public string ScopeObjectId { get; init; } = string.Empty;
}

public sealed record MicroflowExecutionMetadataRef
{
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = "unknown";

    [JsonPropertyName("qualifiedName")]
    public string QualifiedName { get; init; } = string.Empty;

    [JsonPropertyName("sourceObjectId")]
    public string? SourceObjectId { get; init; }

    [JsonPropertyName("sourceActionId")]
    public string? SourceActionId { get; init; }

    [JsonPropertyName("fieldPath")]
    public string? FieldPath { get; init; }

    [JsonPropertyName("required")]
    public bool Required { get; init; } = true;
}

public sealed record MicroflowUnsupportedActionDescriptor
{
    [JsonPropertyName("objectId")]
    public string ObjectId { get; init; } = string.Empty;

    [JsonPropertyName("actionId")]
    public string? ActionId { get; init; }

    [JsonPropertyName("actionKind")]
    public string? ActionKind { get; init; }

    [JsonPropertyName("officialType")]
    public string? OfficialType { get; init; }

    [JsonPropertyName("supportLevel")]
    public string SupportLevel { get; init; } = MicroflowRuntimeSupportLevel.Unsupported;

    [JsonPropertyName("reason")]
    public string Reason { get; init; } = "unsupported";

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("fieldPath")]
    public string? FieldPath { get; init; }
}

public sealed record MicroflowRuntimeDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("schemaId")]
    public string SchemaId { get; init; } = string.Empty;

    [JsonPropertyName("resourceId")]
    public string? ResourceId { get; init; }

    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("schemaVersion")]
    public string? SchemaVersion { get; init; }

    [JsonPropertyName("parameters")]
    public IReadOnlyList<MicroflowExecutionParameter> Parameters { get; init; } = Array.Empty<MicroflowExecutionParameter>();

    [JsonPropertyName("nodes")]
    public IReadOnlyList<MicroflowExecutionNode> Nodes { get; init; } = Array.Empty<MicroflowExecutionNode>();

    [JsonPropertyName("flows")]
    public IReadOnlyList<MicroflowExecutionFlow> Flows { get; init; } = Array.Empty<MicroflowExecutionFlow>();

    [JsonPropertyName("variables")]
    public IReadOnlyList<MicroflowExecutionVariableDeclaration> Variables { get; init; } = Array.Empty<MicroflowExecutionVariableDeclaration>();

    [JsonPropertyName("metadataRefs")]
    public IReadOnlyList<MicroflowExecutionMetadataRef> MetadataRefs { get; init; } = Array.Empty<MicroflowExecutionMetadataRef>();

    [JsonPropertyName("unsupportedActions")]
    public IReadOnlyList<MicroflowUnsupportedActionDescriptor> UnsupportedActions { get; init; } = Array.Empty<MicroflowUnsupportedActionDescriptor>();

    [JsonPropertyName("loopCollections")]
    public IReadOnlyList<MicroflowExecutionLoopCollection> LoopCollections { get; init; } = Array.Empty<MicroflowExecutionLoopCollection>();

    [JsonPropertyName("startNodeId")]
    public string StartNodeId { get; init; } = string.Empty;

    [JsonPropertyName("endNodeIds")]
    public IReadOnlyList<string> EndNodeIds { get; init; } = Array.Empty<string>();

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<MicroflowExecutionDiagnosticDto> Diagnostics { get; init; } = Array.Empty<MicroflowExecutionDiagnosticDto>();

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed record MicroflowActionSupportDescriptor
{
    public string SupportLevel { get; init; } = MicroflowRuntimeSupportLevel.Unsupported;
    public string Reason { get; init; } = "unsupported";
    public string Message { get; init; } = string.Empty;
}
