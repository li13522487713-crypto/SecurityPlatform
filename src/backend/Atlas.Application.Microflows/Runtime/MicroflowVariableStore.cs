using System.Text.Json;
using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Runtime;

public sealed class MicroflowVariableStore : IMicroflowVariableStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly MicroflowVariableScopeStack _scopeStack;
    private readonly List<MicroflowVariableStoreDiagnostic> _diagnostics = [];
    private readonly Func<DateTimeOffset> _utcNow;

    public MicroflowVariableStore(Func<DateTimeOffset>? utcNow = null)
    {
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        _scopeStack = new MicroflowVariableScopeStack();
    }

    public IReadOnlyDictionary<string, MicroflowRuntimeVariableValue> CurrentVariables => _scopeStack.VisibleVariables();

    public IReadOnlyList<MicroflowVariableStoreDiagnostic> Diagnostics => _diagnostics;

    public void Define(MicroflowVariableDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            AddDiagnostic(MicroflowVariableStoreDiagnosticCode.RuntimeUnknownError, "error", "Variable name is required.", definition);
            return;
        }

        if (!definition.AllowShadowing && _scopeStack.VisibleNameExists(definition.Name))
        {
            AddDiagnostic(
                MicroflowVariableStoreDiagnosticCode.RuntimeVariableDuplicated,
                "error",
                $"Variable '{definition.Name}' is already defined in a visible scope.",
                definition);
            return;
        }

        if (definition.AllowShadowing && _scopeStack.VisibleNameExists(definition.Name))
        {
            AddDiagnostic(
                MicroflowVariableStoreDiagnosticCode.RuntimeVariableDuplicated,
                "warning",
                $"Variable '{definition.Name}' shadows a variable from an outer scope.",
                definition);
        }

        var now = _utcNow();
        var value = definition.Value ?? new MicroflowRuntimeVariableValue
        {
            Name = definition.Name,
            DataTypeJson = NormalizeJson(definition.DataTypeJson),
            Kind = InferKind(definition.DataTypeJson, definition.RawValueJson),
            RawValueJson = NormalizeJson(definition.RawValueJson),
            ValuePreview = TrimPreview(definition.ValuePreview ?? Preview(definition.RawValueJson), 200),
            TypePreview = definition.TypePreview ?? CreateTypePreview(definition.DataTypeJson),
            SourceKind = definition.SourceKind,
            SourceObjectId = definition.SourceObjectId,
            SourceActionId = definition.SourceActionId,
            CollectionId = definition.CollectionId,
            LoopObjectId = definition.LoopObjectId,
            Readonly = definition.Readonly,
            System = definition.System,
            ScopeKind = definition.ScopeKind,
            CreatedAt = now,
            UpdatedAt = now
        };

        if (string.Equals(value.Kind, MicroflowRuntimeVariableKind.Unknown, StringComparison.OrdinalIgnoreCase))
        {
            AddDiagnostic(
                MicroflowVariableStoreDiagnosticCode.RuntimeVariableUnknownType,
                "warning",
                $"Variable '{definition.Name}' has unknown runtime type.",
                definition);
        }

        _scopeStack.Current.Variables[definition.Name] = value with
        {
            Name = definition.Name,
            ScopeKind = string.IsNullOrWhiteSpace(value.ScopeKind) ? _scopeStack.Current.Kind : value.ScopeKind,
            CreatedAt = value.CreatedAt == default ? now : value.CreatedAt,
            UpdatedAt = value.UpdatedAt == default ? now : value.UpdatedAt
        };
    }

    public bool Exists(string name)
        => _scopeStack.VisibleNameExists(name);

    public MicroflowRuntimeVariableValue Get(string name)
    {
        if (TryGet(name, out var value))
        {
            return value!;
        }

        var diagnostic = AddDiagnostic(
            MicroflowVariableStoreDiagnosticCode.RuntimeVariableNotFound,
            "error",
            $"Variable '{name}' was not found.",
            variableName: name);
        throw new MicroflowVariableStoreException(diagnostic);
    }

    public bool TryGet(string name, out MicroflowRuntimeVariableValue? value)
    {
        if (_scopeStack.TryGet(name, out value, out _))
        {
            return true;
        }

        value = null;
        return false;
    }

    public void Set(string name, MicroflowRuntimeVariableValue value)
    {
        if (!_scopeStack.TryGet(name, out var existing, out var scope) || existing is null || scope is null)
        {
            var diagnostic = AddDiagnostic(
                MicroflowVariableStoreDiagnosticCode.RuntimeVariableNotFound,
                "error",
                $"Variable '{name}' was not found.",
                variableName: name);
            throw new MicroflowVariableStoreException(diagnostic);
        }

        if (existing.Readonly || existing.System)
        {
            var diagnostic = AddDiagnostic(
                MicroflowVariableStoreDiagnosticCode.RuntimeVariableReadonly,
                "error",
                $"Variable '{name}' is readonly and cannot be modified.",
                existing);
            throw new MicroflowVariableStoreException(diagnostic);
        }

        if (!Compatible(existing.DataTypeJson, value.DataTypeJson))
        {
            AddDiagnostic(
                MicroflowVariableStoreDiagnosticCode.RuntimeVariableTypeMismatch,
                "warning",
                $"Variable '{name}' is assigned with a value whose type does not match the declaration.",
                existing);
        }

        scope.Variables[name] = value with
        {
            Name = name,
            DataTypeJson = NormalizeJson(value.DataTypeJson ?? existing.DataTypeJson),
            Kind = string.IsNullOrWhiteSpace(value.Kind) || string.Equals(value.Kind, MicroflowRuntimeVariableKind.Unknown, StringComparison.OrdinalIgnoreCase)
                ? existing.Kind
                : value.Kind,
            RawValueJson = NormalizeJson(value.RawValueJson),
            ValuePreview = TrimPreview(value.ValuePreview, 200),
            TypePreview = string.IsNullOrWhiteSpace(value.TypePreview) ? existing.TypePreview : value.TypePreview,
            SourceObjectId = value.SourceObjectId ?? existing.SourceObjectId,
            SourceActionId = value.SourceActionId ?? existing.SourceActionId,
            CollectionId = value.CollectionId ?? existing.CollectionId,
            LoopObjectId = value.LoopObjectId ?? existing.LoopObjectId,
            Readonly = existing.Readonly,
            System = existing.System,
            ScopeKind = existing.ScopeKind,
            CreatedAt = existing.CreatedAt,
            UpdatedAt = _utcNow()
        };
    }

    public void Remove(string name)
    {
        if (!_scopeStack.TryGet(name, out var existing, out var scope) || existing is null || scope is null)
        {
            var diagnostic = AddDiagnostic(
                MicroflowVariableStoreDiagnosticCode.RuntimeVariableNotFound,
                "error",
                $"Variable '{name}' was not found.",
                variableName: name);
            throw new MicroflowVariableStoreException(diagnostic);
        }

        if (existing.System)
        {
            var diagnostic = AddDiagnostic(
                MicroflowVariableStoreDiagnosticCode.RuntimeVariableReadonly,
                "error",
                $"System variable '{name}' cannot be removed.",
                existing);
            throw new MicroflowVariableStoreException(diagnostic);
        }

        scope.Variables.Remove(name);
    }

    public IDisposable PushScope(MicroflowVariableScopeFrame frame)
    {
        var pushed = _scopeStack.Push(frame);
        return new ScopeLease(this, pushed.Id);
    }

    public MicroflowVariableStoreSnapshot CreateSnapshot(MicroflowVariableSnapshotOptions options)
    {
        var variables = CurrentVariables.Values
            .Where(variable => options.IncludeSystem || !variable.System)
            .Where(variable => string.IsNullOrWhiteSpace(options.ScopeFilter) || string.Equals(variable.ScopeKind, options.ScopeFilter, StringComparison.OrdinalIgnoreCase))
            .Where(variable => string.IsNullOrWhiteSpace(options.VariableNameFilter) || variable.Name.Contains(options.VariableNameFilter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(variable => variable.System ? 0 : 1)
            .ThenBy(variable => variable.Name, StringComparer.Ordinal)
            .ToDictionary(
                variable => variable.Name,
                variable => variable with
                {
                    RawValueJson = options.IncludeRawValue ? variable.RawValueJson : null,
                    ValuePreview = TrimPreview(variable.ValuePreview, options.MaxValuePreviewLength)
                },
                StringComparer.Ordinal);

        return new MicroflowVariableStoreSnapshot
        {
            ObjectId = options.ObjectId,
            ActionId = options.ActionId,
            CollectionId = options.CollectionId,
            StepIndex = options.StepIndex,
            Variables = variables,
            Diagnostics = _diagnostics.ToArray()
        };
    }

    public MicroflowVariableStoreDiagnostic ReportDiagnostic(
        string code,
        string severity,
        string message,
        string? variableName = null,
        string? objectId = null,
        string? actionId = null,
        string? collectionId = null,
        string? scopeKind = null)
        => AddDiagnostic(code, severity, message, variableName, objectId, actionId, collectionId, scopeKind);

    public static string? ToJson(JsonElement? element)
        => element.HasValue ? NormalizeJson(element.Value.GetRawText()) : null;

    public static string ToJson<T>(T value)
        => JsonSerializer.Serialize(value, JsonOptions);

    public static JsonElement? ToJsonElement(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(json).Clone();
        }
        catch (JsonException)
        {
            return JsonSerializer.SerializeToElement(json, JsonOptions);
        }
    }

    public static string Preview(string? rawValueJson, int maxLength = 200)
    {
        if (string.IsNullOrWhiteSpace(rawValueJson))
        {
            return "null";
        }

        try
        {
            using var document = JsonDocument.Parse(rawValueJson);
            var preview = document.RootElement.ValueKind switch
            {
                JsonValueKind.String => document.RootElement.GetString() ?? string.Empty,
                JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => document.RootElement.GetRawText(),
                JsonValueKind.Null or JsonValueKind.Undefined => "null",
                _ => document.RootElement.GetRawText()
            };
            return TrimPreview(preview, maxLength);
        }
        catch (JsonException)
        {
            return TrimPreview(rawValueJson, maxLength);
        }
    }

    public static string TrimPreview(string? value, int maxLength)
    {
        var preview = string.IsNullOrEmpty(value) ? "null" : value;
        var limit = maxLength <= 0 ? 200 : maxLength;
        return preview.Length > limit ? preview[..limit] + "..." : preview;
    }

    public static string InferKind(string? dataTypeJson, string? rawValueJson = null)
    {
        var kind = ReadKind(dataTypeJson);
        if (!string.IsNullOrWhiteSpace(kind))
        {
            return kind switch
            {
                "integer" or "decimal" or "boolean" or "string" or "dateTime" => MicroflowRuntimeVariableKind.Primitive,
                "object" => MicroflowRuntimeVariableKind.Object,
                "list" => MicroflowRuntimeVariableKind.List,
                "enumeration" => MicroflowRuntimeVariableKind.Enumeration,
                "json" => MicroflowRuntimeVariableKind.Json,
                "error" => MicroflowRuntimeVariableKind.Error,
                "httpResponse" => MicroflowRuntimeVariableKind.HttpResponse,
                _ => MicroflowRuntimeVariableKind.Unknown
            };
        }

        if (string.IsNullOrWhiteSpace(rawValueJson))
        {
            return MicroflowRuntimeVariableKind.Unknown;
        }

        try
        {
            using var document = JsonDocument.Parse(rawValueJson);
            return document.RootElement.ValueKind switch
            {
                JsonValueKind.Array => MicroflowRuntimeVariableKind.List,
                JsonValueKind.Object => MicroflowRuntimeVariableKind.Object,
                JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => MicroflowRuntimeVariableKind.Primitive,
                _ => MicroflowRuntimeVariableKind.Unknown
            };
        }
        catch (JsonException)
        {
            return MicroflowRuntimeVariableKind.Unknown;
        }
    }

    public static string CreateTypePreview(string? dataTypeJson)
    {
        if (string.IsNullOrWhiteSpace(dataTypeJson))
        {
            return MicroflowRuntimeVariableKind.Unknown;
        }

        try
        {
            using var document = JsonDocument.Parse(dataTypeJson);
            return CreateTypePreview(document.RootElement);
        }
        catch (JsonException)
        {
            return MicroflowRuntimeVariableKind.Unknown;
        }
    }

    private static string CreateTypePreview(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return MicroflowRuntimeVariableKind.Unknown;
        }

        var kind = ReadKind(element.GetRawText());
        return kind switch
        {
            "object" => $"Object<{ReadString(element, "entityQualifiedName") ?? "unknown"}>",
            "list" when element.TryGetProperty("itemType", out var itemType) => $"List<{ReadString(itemType, "entityQualifiedName") ?? CreateTypePreview(itemType)}>",
            "enumeration" => $"Enum<{ReadString(element, "enumerationQualifiedName") ?? ReadString(element, "enumQualifiedName") ?? "unknown"}>",
            "boolean" or "string" or "integer" or "long" or "decimal" or "dateTime" or "json" or "binary" or "void" => kind,
            _ => MicroflowRuntimeVariableKind.Unknown
        };
    }

    private static string? NormalizeJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.GetRawText();
        }
        catch (JsonException)
        {
            return JsonSerializer.Serialize(json, JsonOptions);
        }
    }

    private static bool Compatible(string? expectedJson, string? actualJson)
    {
        var expected = ReadKind(expectedJson);
        var actual = ReadKind(actualJson);
        return string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(actual) || string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadKind(string? dataTypeJson)
    {
        if (string.IsNullOrWhiteSpace(dataTypeJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(dataTypeJson);
            return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("kind", out var kind)
                ? kind.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty(propertyName, out var property)
           && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private MicroflowVariableStoreDiagnostic AddDiagnostic(string code, string severity, string message, MicroflowVariableDefinition definition)
        => AddDiagnostic(code, severity, message, definition.Name, definition.SourceObjectId, definition.SourceActionId, definition.CollectionId, definition.ScopeKind);

    private MicroflowVariableStoreDiagnostic AddDiagnostic(string code, string severity, string message, MicroflowRuntimeVariableValue value)
        => AddDiagnostic(code, severity, message, value.Name, value.SourceObjectId, value.SourceActionId, value.CollectionId, value.ScopeKind);

    private MicroflowVariableStoreDiagnostic AddDiagnostic(
        string code,
        string severity,
        string message,
        string? variableName = null,
        string? objectId = null,
        string? actionId = null,
        string? collectionId = null,
        string? scopeKind = null)
    {
        var diagnostic = new MicroflowVariableStoreDiagnostic
        {
            Code = code,
            Severity = severity,
            Message = message,
            VariableName = variableName,
            ObjectId = objectId,
            ActionId = actionId,
            CollectionId = collectionId,
            ScopeKind = scopeKind
        };
        _diagnostics.Add(diagnostic);
        return diagnostic;
    }

    private sealed class ScopeLease : IDisposable
    {
        private readonly MicroflowVariableStore _store;
        private readonly string _frameId;
        private bool _disposed;

        public ScopeLease(MicroflowVariableStore store, string frameId)
        {
            _store = store;
            _frameId = frameId;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (!_store._scopeStack.Pop(_frameId))
            {
                _store.AddDiagnostic(
                    MicroflowVariableStoreDiagnosticCode.RuntimeVariableScopeError,
                    "error",
                    $"Variable scope '{_frameId}' cannot be popped because it is not the current scope.");
            }
        }
    }
}
