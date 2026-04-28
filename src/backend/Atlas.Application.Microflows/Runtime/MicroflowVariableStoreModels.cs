using System.Text.Json;
using System.Text.Json.Serialization;
using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Runtime;

public static class MicroflowRuntimeVariableKind
{
    public const string Primitive = "primitive";
    public const string Object = "object";
    public const string List = "list";
    public const string Enumeration = "enumeration";
    public const string Json = "json";
    public const string Error = "error";
    public const string HttpResponse = "httpResponse";
    public const string Unknown = "unknown";
}

public static class MicroflowVariableSourceKind
{
    public const string Parameter = "parameter";
    public const string ActionOutput = "actionOutput";
    public const string LocalVariable = "localVariable";
    public const string LoopIterator = "loopIterator";
    public const string System = "system";
    public const string ErrorContext = "errorContext";
    public const string RestResponse = "restResponse";
    public const string MicroflowReturn = "microflowReturn";
    public const string ModeledOnly = "modeledOnly";
    public const string Unknown = "unknown";
}

public static class MicroflowVariableScopeKind
{
    public const string Global = "global";
    public const string Action = "action";
    public const string Loop = "loop";
    public const string ErrorHandler = "errorHandler";
    public const string Call = "call";
    public const string System = "system";
    public const string Downstream = "downstream";
}

public static class MicroflowVariableStoreDiagnosticCode
{
    public const string RuntimeVariableNotFound = RuntimeErrorCode.RuntimeVariableNotFound;
    public const string RuntimeVariableTypeMismatch = RuntimeErrorCode.RuntimeVariableTypeMismatch;
    public const string RuntimeVariableReadonly = "RUNTIME_VARIABLE_READONLY";
    public const string RuntimeVariableDuplicated = "RUNTIME_VARIABLE_DUPLICATED";
    public const string RuntimeVariableScopeError = "RUNTIME_VARIABLE_SCOPE_ERROR";
    public const string RuntimeUnknownError = RuntimeErrorCode.RuntimeUnknownError;
    public const string RuntimeParameterMissing = "RUNTIME_PARAMETER_MISSING";
    public const string RuntimeInputExtra = "RUNTIME_INPUT_EXTRA";
    public const string RuntimeVariableUnknownType = "RUNTIME_VARIABLE_UNKNOWN_TYPE";
}

public sealed record MicroflowRuntimeVariableValue
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("dataTypeJson")]
    public string? DataTypeJson { get; init; }

    [JsonPropertyName("kind")]
    public string Kind { get; init; } = MicroflowRuntimeVariableKind.Unknown;

    [JsonPropertyName("rawValueJson")]
    public string? RawValueJson { get; init; }

    [JsonPropertyName("valuePreview")]
    public string ValuePreview { get; init; } = "null";

    [JsonPropertyName("typePreview")]
    public string TypePreview { get; init; } = MicroflowRuntimeVariableKind.Unknown;

    [JsonPropertyName("sourceKind")]
    public string SourceKind { get; init; } = MicroflowVariableSourceKind.Unknown;

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

    [JsonPropertyName("system")]
    public bool System { get; init; }

    [JsonPropertyName("scopeKind")]
    public string ScopeKind { get; init; } = MicroflowVariableScopeKind.Global;

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed record MicroflowVariableDefinition
{
    public string Name { get; init; } = string.Empty;
    public string? DataTypeJson { get; init; }
    public MicroflowRuntimeVariableValue? Value { get; init; }
    public string? RawValueJson { get; init; }
    public string? ValuePreview { get; init; }
    public string? TypePreview { get; init; }
    public string SourceKind { get; init; } = MicroflowVariableSourceKind.Unknown;
    public string? SourceObjectId { get; init; }
    public string? SourceActionId { get; init; }
    public string? CollectionId { get; init; }
    public string? LoopObjectId { get; init; }
    public string ScopeKind { get; init; } = MicroflowVariableScopeKind.Global;
    public bool Readonly { get; init; }
    public bool System { get; init; }
    public int? DeclaredAtStep { get; init; }
    public string? Documentation { get; init; }
    public bool AllowShadowing { get; init; }
}

public sealed record MicroflowVariableScopeFrame
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Kind { get; init; } = MicroflowVariableScopeKind.Action;
    public string? CollectionId { get; init; }
    public string? ObjectId { get; init; }
    public string? ActionId { get; init; }
    public string? LoopObjectId { get; init; }
    public string? ErrorHandlerFlowId { get; init; }
    public string? CallFrameId { get; init; }
    public string? ParentId { get; init; }
    public Dictionary<string, MicroflowRuntimeVariableValue> Variables { get; init; } = new(StringComparer.Ordinal);
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record MicroflowVariableStoreSnapshot
{
    public string? ObjectId { get; init; }
    public string? ActionId { get; init; }
    public string? CollectionId { get; init; }
    public int StepIndex { get; init; }
    public IReadOnlyDictionary<string, MicroflowRuntimeVariableValue> Variables { get; init; } = new Dictionary<string, MicroflowRuntimeVariableValue>();
    public IReadOnlyList<MicroflowVariableStoreDiagnostic> Diagnostics { get; init; } = Array.Empty<MicroflowVariableStoreDiagnostic>();
}

public sealed record MicroflowVariableSnapshotOptions
{
    public bool IncludeSystem { get; init; } = true;
    public bool IncludeRawValue { get; init; } = true;
    public bool IncludeReadonly { get; init; } = true;
    public int MaxValuePreviewLength { get; init; } = 200;
    public string? ScopeFilter { get; init; }
    public string? VariableNameFilter { get; init; }
    public string? ObjectId { get; init; }
    public string? ActionId { get; init; }
    public string? CollectionId { get; init; }
    public int StepIndex { get; init; }
}

public sealed record MicroflowVariableStoreDiagnostic
{
    public string Code { get; init; } = MicroflowVariableStoreDiagnosticCode.RuntimeUnknownError;
    public string Severity { get; init; } = "error";
    public string Message { get; init; } = string.Empty;
    public string? VariableName { get; init; }
    public string? ObjectId { get; init; }
    public string? ActionId { get; init; }
    public string? CollectionId { get; init; }
    public string? ScopeKind { get; init; }
}

public sealed class MicroflowVariableStoreException : Exception
{
    public MicroflowVariableStoreException(MicroflowVariableStoreDiagnostic diagnostic)
        : base(diagnostic.Message)
    {
        Diagnostic = diagnostic;
    }

    public MicroflowVariableStoreDiagnostic Diagnostic { get; }
}
