using System.Text.Json;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime.Security;

namespace Atlas.Application.Microflows.Runtime;

public static class MicroflowRuntimeExecutionMode
{
    public const string DryRun = "dryRun";
    public const string TestRun = "testRun";
    public const string PublishedRun = "publishedRun";
    public const string PreviewRun = "previewRun";
}

public sealed class RuntimeExecutionContext
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private RuntimeExecutionContext(
        string runId,
        MicroflowExecutionPlan executionPlan,
        IMicroflowVariableStore variableStore,
        DateTimeOffset startedAt)
    {
        RunId = runId;
        ResourceId = executionPlan.ResourceId;
        SchemaId = executionPlan.SchemaId;
        Version = executionPlan.Version;
        ExecutionPlan = executionPlan;
        VariableStore = variableStore;
        StartedAt = startedAt;
        MetadataVersion = executionPlan.SchemaVersion;
    }

    public string RunId { get; }
    public string? ResourceId { get; init; }
    public string SchemaId { get; init; } = string.Empty;
    public string? Version { get; init; }
    public string Mode { get; init; } = MicroflowRuntimeExecutionMode.DryRun;
    public MicroflowExecutionPlan ExecutionPlan { get; }
    public IMicroflowVariableStore VariableStore { get; }
    public string? CurrentNodeId { get; set; }
    public string? CurrentFlowId { get; set; }
    public string? CurrentCollectionId { get; set; }
    public string? CurrentLoopObjectId { get; set; }
    public int StepIndex { get; set; }
    public Stack<string> CallStack { get; } = new();
    public Stack<MicroflowVariableScopeFrame> LoopStack { get; } = new();
    public Stack<MicroflowVariableScopeFrame> ErrorStack { get; } = new();
    public object? TransactionContext { get; init; }
    public MicroflowRequestContext SecurityContext { get; init; } = new();
    public MicroflowRuntimeSecurityContext RuntimeSecurityContext { get; init; } = MicroflowRuntimeSecurityContext.System();
    public string? MetadataVersion { get; init; }
    public DateTimeOffset StartedAt { get; }
    public IReadOnlyList<MicroflowVariableStoreDiagnostic> Diagnostics => VariableStore.Diagnostics;

    public static RuntimeExecutionContext Create(
        string runId,
        MicroflowExecutionPlan executionPlan,
        string mode,
        IReadOnlyDictionary<string, JsonElement>? input,
        MicroflowRequestContext? securityContext,
        DateTimeOffset startedAt)
    {
        var store = new MicroflowVariableStore(() => DateTimeOffset.UtcNow);
        var context = new RuntimeExecutionContext(runId, executionPlan, store, startedAt)
        {
            Mode = mode,
            SecurityContext = securityContext ?? new MicroflowRequestContext(),
            RuntimeSecurityContext = MicroflowRuntimeSecurityContext.FromRequestContext(securityContext, applyEntityAccess: true)
        };

        context.InitializeParameters(input ?? new Dictionary<string, JsonElement>());
        context.InitializeSystemVariables();
        return context;
    }

    public IDisposable PushLoopScope(
        string loopObjectId,
        string collectionId,
        string? iteratorVariableName,
        int index,
        JsonElement? iteratorRawValue,
        string? iteratorPreview = null)
    {
        var frame = new MicroflowVariableScopeFrame
        {
            Kind = MicroflowVariableScopeKind.Loop,
            CollectionId = collectionId,
            LoopObjectId = loopObjectId,
            ObjectId = loopObjectId
        };
        var lease = VariableStore.PushScope(frame);
        LoopStack.Push(frame);
        var iteratorName = string.IsNullOrWhiteSpace(iteratorVariableName) ? "$iterator" : iteratorVariableName!;
        VariableStore.Define(new MicroflowVariableDefinition
        {
            Name = iteratorName,
            DataTypeJson = JsonSerializer.Serialize(new { kind = "object" }, JsonOptions),
            RawValueJson = iteratorRawValue.HasValue
                ? iteratorRawValue.Value.GetRawText()
                : JsonSerializer.Serialize(new { id = $"{loopObjectId}-item-{index}", index }, JsonOptions),
            ValuePreview = iteratorPreview ?? $"{iteratorName}[{index}]",
            SourceKind = MicroflowVariableSourceKind.LoopIterator,
            CollectionId = collectionId,
            LoopObjectId = loopObjectId,
            ScopeKind = MicroflowVariableScopeKind.Loop,
            Readonly = true,
            AllowShadowing = true
        });
        VariableStore.Define(new MicroflowVariableDefinition
        {
            Name = "$currentIndex",
            DataTypeJson = JsonSerializer.Serialize(new { kind = "integer" }, JsonOptions),
            RawValueJson = index.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ValuePreview = index.ToString(System.Globalization.CultureInfo.InvariantCulture),
            SourceKind = MicroflowVariableSourceKind.System,
            CollectionId = collectionId,
            LoopObjectId = loopObjectId,
            ScopeKind = MicroflowVariableScopeKind.Loop,
            Readonly = true,
            System = true,
            AllowShadowing = true
        });
        return new RuntimeScopeLease(lease, () =>
        {
            if (LoopStack.Count > 0)
            {
                LoopStack.Pop();
            }
        });
    }

    public IDisposable PushErrorHandlerScope(
        MicroflowRuntimeErrorDto error,
        string? errorHandlerFlowId,
        JsonElement? latestHttpResponse = null)
    {
        var frame = new MicroflowVariableScopeFrame
        {
            Kind = MicroflowVariableScopeKind.ErrorHandler,
            ObjectId = error.ObjectId,
            ActionId = error.ActionId,
            ErrorHandlerFlowId = errorHandlerFlowId
        };
        var lease = VariableStore.PushScope(frame);
        ErrorStack.Push(frame);
        VariableStore.Define(new MicroflowVariableDefinition
        {
            Name = "$latestError",
            DataTypeJson = JsonSerializer.Serialize(new { kind = "error" }, JsonOptions),
            RawValueJson = JsonSerializer.Serialize(error, JsonOptions),
            ValuePreview = MicroflowVariableStore.TrimPreview($"{error.Code}: {error.Message}", 200),
            SourceKind = MicroflowVariableSourceKind.ErrorContext,
            SourceObjectId = error.ObjectId,
            SourceActionId = error.ActionId,
            ScopeKind = MicroflowVariableScopeKind.ErrorHandler,
            Readonly = true
        });
        if (latestHttpResponse.HasValue)
        {
            VariableStore.Define(new MicroflowVariableDefinition
            {
                Name = "$latestHttpResponse",
                DataTypeJson = JsonSerializer.Serialize(new { kind = "httpResponse" }, JsonOptions),
                RawValueJson = latestHttpResponse.Value.GetRawText(),
                ValuePreview = latestHttpResponse.Value.TryGetProperty("statusCode", out var statusCode)
                    ? $"HTTP {statusCode.GetRawText()}"
                    : "HTTP response",
                SourceKind = MicroflowVariableSourceKind.RestResponse,
                SourceObjectId = error.ObjectId,
                SourceActionId = error.ActionId,
                ScopeKind = MicroflowVariableScopeKind.ErrorHandler,
                Readonly = true
            });
        }

        return new RuntimeScopeLease(lease, () =>
        {
            if (ErrorStack.Count > 0)
            {
                ErrorStack.Pop();
            }
        });
    }

    public MicroflowVariableStoreSnapshot CreateSnapshot(
        string? objectId,
        string? actionId,
        string? collectionId,
        int stepIndex,
        bool includeSystem = true,
        bool includeRawValue = true)
        => VariableStore.CreateSnapshot(new MicroflowVariableSnapshotOptions
        {
            ObjectId = objectId,
            ActionId = actionId,
            CollectionId = collectionId,
            StepIndex = stepIndex,
            IncludeSystem = includeSystem,
            IncludeRawValue = includeRawValue,
            MaxValuePreviewLength = 200
        });

    private void InitializeParameters(IReadOnlyDictionary<string, JsonElement> input)
    {
        var parameterNames = new HashSet<string>(ExecutionPlan.Parameters.Select(parameter => parameter.Name), StringComparer.Ordinal);
        foreach (var parameter in ExecutionPlan.Parameters)
        {
            if (string.IsNullOrWhiteSpace(parameter.Name))
            {
                continue;
            }

            var hasInput = input.TryGetValue(parameter.Name, out var rawValue);
            if (!hasInput && parameter.Required)
            {
                VariableStore.Define(new MicroflowVariableDefinition
                {
                    Name = parameter.Name,
                    DataTypeJson = parameter.DataTypeJson.GetRawText(),
                    RawValueJson = "null",
                    ValuePreview = "null",
                    SourceKind = MicroflowVariableSourceKind.Parameter,
                    ScopeKind = MicroflowVariableScopeKind.Global,
                    Readonly = true,
                    Documentation = parameter.Documentation
                });
                AddParameterDiagnostic(parameter.Name, "required parameter input is missing");
                continue;
            }

            VariableStore.Define(new MicroflowVariableDefinition
            {
                Name = parameter.Name,
                DataTypeJson = parameter.DataTypeJson.GetRawText(),
                RawValueJson = hasInput ? rawValue.GetRawText() : "null",
                ValuePreview = hasInput ? MicroflowVariableStore.Preview(rawValue.GetRawText()) : "null",
                SourceKind = MicroflowVariableSourceKind.Parameter,
                ScopeKind = MicroflowVariableScopeKind.Global,
                Readonly = true,
                Documentation = parameter.Documentation
            });
        }

        foreach (var extra in input.Keys.Where(key => !parameterNames.Contains(key)))
        {
            AddParameterDiagnostic(extra, "input value does not match any ExecutionPlan parameter", severity: "warning", code: MicroflowVariableStoreDiagnosticCode.RuntimeInputExtra);
        }
    }

    private void InitializeSystemVariables()
    {
        var userId = SecurityContext.UserId ?? "anonymous";
        var userName = SecurityContext.UserName ?? userId;
        var raw = JsonSerializer.Serialize(new
        {
            userId,
            userName,
            roles = SecurityContext.Roles
        }, JsonOptions);
        VariableStore.Define(new MicroflowVariableDefinition
        {
            Name = "$currentUser",
            DataTypeJson = JsonSerializer.Serialize(new { kind = "object", entityQualifiedName = "System.User" }, JsonOptions),
            RawValueJson = raw,
            ValuePreview = userName,
            SourceKind = MicroflowVariableSourceKind.System,
            ScopeKind = MicroflowVariableScopeKind.System,
            Readonly = true,
            System = true
        });
    }

    private void AddParameterDiagnostic(string variableName, string message, string severity = "error", string code = MicroflowVariableStoreDiagnosticCode.RuntimeParameterMissing)
    {
        if (VariableStore is MicroflowVariableStore concrete)
        {
            concrete.ReportDiagnostic(code, severity, message, variableName, scopeKind: MicroflowVariableScopeKind.Global);
        }
    }

    private sealed class RuntimeScopeLease : IDisposable
    {
        private readonly IDisposable _inner;
        private readonly Action _onDispose;
        private bool _disposed;

        public RuntimeScopeLease(IDisposable inner, Action onDispose)
        {
            _inner = inner;
            _onDispose = onDispose;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _onDispose();
            _inner.Dispose();
        }
    }
}
