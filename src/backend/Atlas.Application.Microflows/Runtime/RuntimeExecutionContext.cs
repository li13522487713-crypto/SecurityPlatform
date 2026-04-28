using System.Text.Json;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime.Calls;
using Atlas.Application.Microflows.Runtime.ErrorHandling;
using Atlas.Application.Microflows.Runtime.Security;
using Atlas.Application.Microflows.Runtime.Transactions;

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
    public List<MicroflowCallStackFrame> CallStackFrames { get; } = [];
    public MicroflowCallStackFrame? CurrentCallFrame { get; set; }
    public int MaxCallDepth { get; init; } = 10;
    public string CallCorrelationId { get; init; } = Guid.NewGuid().ToString("N");
    public string? ParentRunId { get; init; }
    public string RootRunId { get; init; } = string.Empty;
    public Stack<MicroflowVariableScopeFrame> LoopStack { get; } = new();
    public Stack<MicroflowVariableScopeFrame> ErrorStack { get; } = new();
    public MicroflowRuntimeTransactionContext? Transaction { get; set; }
    public IMicroflowUnitOfWork? UnitOfWork { get; set; }
    public IMicroflowTransactionManager? TransactionManager { get; set; }
    public MicroflowRuntimeTransactionOptions? TransactionOptions { get; set; }
    public string? CurrentTransactionId => Transaction?.Id;
    public IReadOnlyList<MicroflowRuntimeTransactionDiagnostic> TransactionDiagnostics => Transaction?.Diagnostics.ToArray() ?? Array.Empty<MicroflowRuntimeTransactionDiagnostic>();
    public object? TransactionContext => Transaction;
    public MicroflowErrorHandlingSummary ErrorHandlingSummary => new()
    {
        HandledErrorCount = _handledErrorCount,
        UnhandledErrorCount = _unhandledErrorCount,
        ContinuedErrorCount = _continuedErrorCount,
        RollbackCount = _rollbackCount,
        CustomHandlerCount = _customHandlerCount,
        ErrorEventCount = _errorEventCount,
        LatestErrorPreview = _latestErrorPreview
    };
    public MicroflowRequestContext SecurityContext { get; init; } = new();
    public MicroflowRuntimeSecurityContext RuntimeSecurityContext { get; init; } = MicroflowRuntimeSecurityContext.System();
    public MicroflowMetadataCatalogDto? MetadataCatalog { get; init; }
    public string? MetadataVersion { get; init; }
    public DateTimeOffset StartedAt { get; }
    public IReadOnlyList<MicroflowVariableStoreDiagnostic> Diagnostics => VariableStore.Diagnostics;
    private int _handledErrorCount;
    private int _unhandledErrorCount;
    private int _continuedErrorCount;
    private int _rollbackCount;
    private int _customHandlerCount;
    private int _errorEventCount;
    private string? _latestErrorPreview;

    public static RuntimeExecutionContext Create(
        string runId,
        MicroflowExecutionPlan executionPlan,
        string mode,
        IReadOnlyDictionary<string, JsonElement>? input,
        MicroflowRequestContext? securityContext,
        DateTimeOffset startedAt,
        IMicroflowTransactionManager? transactionManager = null,
        MicroflowRuntimeTransactionOptions? transactionOptions = null,
        string? parentRunId = null,
        string? rootRunId = null,
        string? callCorrelationId = null,
        int maxCallDepth = 10,
        MicroflowMetadataCatalogDto? metadataCatalog = null,
        MicroflowCallStackFrame? currentCallFrame = null,
        IReadOnlyList<MicroflowCallStackFrame>? callStackFrames = null,
        IMicroflowVariableStore? variableStore = null)
    {
        var store = variableStore ?? new MicroflowVariableStore(() => DateTimeOffset.UtcNow);
        var context = new RuntimeExecutionContext(runId, executionPlan, store, startedAt)
        {
            Mode = mode,
            SecurityContext = securityContext ?? new MicroflowRequestContext(),
            RuntimeSecurityContext = MicroflowRuntimeSecurityContext.FromRequestContext(securityContext, applyEntityAccess: true),
            TransactionManager = transactionManager,
            TransactionOptions = transactionOptions,
            ParentRunId = parentRunId,
            RootRunId = string.IsNullOrWhiteSpace(rootRunId) ? runId : rootRunId!,
            CallCorrelationId = string.IsNullOrWhiteSpace(callCorrelationId) ? Guid.NewGuid().ToString("N") : callCorrelationId!,
            MaxCallDepth = maxCallDepth,
            MetadataCatalog = metadataCatalog,
            CurrentCallFrame = currentCallFrame
        };
        if (callStackFrames is not null)
        {
            context.CallStackFrames.AddRange(callStackFrames);
        }

        // When the caller supplied an existing store (e.g. the engine reusing its
        // own MicroflowVariableStore), assume parameters and system variables were
        // already initialised in the prior context to avoid duplicating them.
        if (variableStore is null)
        {
            context.InitializeParameters(input ?? new Dictionary<string, JsonElement>());
            context.InitializeSystemVariables();
        }

        if (transactionManager is not null && (transactionOptions ?? new MicroflowRuntimeTransactionOptions()).AutoBegin)
        {
            transactionManager.Begin(context, transactionOptions ?? new MicroflowRuntimeTransactionOptions());
        }

        return context;
    }

    public IDisposable PushLoopScope(
        string loopObjectId,
        string collectionId,
        string? iteratorVariableName,
        int index,
        JsonElement? iteratorRawValue,
        string? iteratorPreview = null,
        string? iteratorDataTypeJson = null,
        bool defineIterator = true)
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
        if (defineIterator)
        {
            VariableStore.Define(new MicroflowVariableDefinition
            {
                Name = iteratorName,
                DataTypeJson = string.IsNullOrWhiteSpace(iteratorDataTypeJson)
                    ? JsonSerializer.Serialize(new { kind = "object" }, JsonOptions)
                    : iteratorDataTypeJson,
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
        }
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
        JsonElement? latestHttpResponse = null,
        JsonElement? latestSoapFault = null)
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
            Readonly = true,
            System = true,
            AllowShadowing = true
        });
        if (latestHttpResponse.HasValue)
        {
            VariableStore.Define(new MicroflowVariableDefinition
            {
                Name = "$latestHttpResponse",
                DataTypeJson = JsonSerializer.Serialize(new { kind = "httpResponse" }, JsonOptions),
                RawValueJson = latestHttpResponse.Value.GetRawText(),
                ValuePreview = latestHttpResponse.Value.TryGetProperty("statusCode", out var statusCode)
                    ? $"HTTP {statusCode.GetRawText()} {ReadOptionalString(latestHttpResponse.Value, "reasonPhrase")}".Trim()
                    : "HTTP response",
                SourceKind = MicroflowVariableSourceKind.RestResponse,
                SourceObjectId = error.ObjectId,
                SourceActionId = error.ActionId,
                ScopeKind = MicroflowVariableScopeKind.ErrorHandler,
                Readonly = true,
                System = true,
                AllowShadowing = true
            });
        }
        if (latestSoapFault.HasValue)
        {
            VariableStore.Define(new MicroflowVariableDefinition
            {
                Name = "$latestSoapFault",
                DataTypeJson = JsonSerializer.Serialize(new { kind = "soapFault" }, JsonOptions),
                RawValueJson = latestSoapFault.Value.GetRawText(),
                ValuePreview = latestSoapFault.Value.TryGetProperty("faultCode", out var faultCode)
                    ? $"SOAP Fault {faultCode.GetRawText()}".Trim()
                    : "SOAP fault",
                SourceKind = MicroflowVariableSourceKind.ErrorContext,
                SourceObjectId = error.ObjectId,
                SourceActionId = error.ActionId,
                ScopeKind = MicroflowVariableScopeKind.ErrorHandler,
                Readonly = true,
                System = true,
                AllowShadowing = true
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

    public void RecordHandledError(MicroflowRuntimeErrorDto error)
    {
        _handledErrorCount++;
        SetLatestErrorPreview(error);
    }

    public void RecordUnhandledError(MicroflowRuntimeErrorDto error)
    {
        _unhandledErrorCount++;
        SetLatestErrorPreview(error);
    }

    public void RecordContinuedError(MicroflowRuntimeErrorDto error)
    {
        _continuedErrorCount++;
        SetLatestErrorPreview(error);
    }

    public void RecordRollback(MicroflowRuntimeErrorDto error)
    {
        _rollbackCount++;
        SetLatestErrorPreview(error);
    }

    public void RecordCustomHandler(MicroflowRuntimeErrorDto error)
    {
        _customHandlerCount++;
        SetLatestErrorPreview(error);
    }

    public void RecordErrorEvent(MicroflowRuntimeErrorDto error)
    {
        _errorEventCount++;
        SetLatestErrorPreview(error);
    }

    private void SetLatestErrorPreview(MicroflowRuntimeErrorDto error)
        => _latestErrorPreview = MicroflowVariableStore.TrimPreview($"{error.Code}: {error.Message}", 200);

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

    public MicroflowRuntimeTransactionSnapshot CreateTransactionSnapshot(
        string? operation = null,
        int maxChangedObjectPreviewCount = 10)
        => TransactionManager?.CreateSnapshot(
            this,
            new MicroflowRuntimeTransactionSnapshotOptions
            {
                Operation = operation,
                MaxChangedObjectPreviewCount = maxChangedObjectPreviewCount
            })
           ?? new MicroflowRuntimeTransactionSnapshot
           {
               Operation = operation,
               Status = MicroflowRuntimeTransactionStatus.None,
               Mode = MicroflowRuntimeTransactionMode.None
           };

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

    private static string? ReadOptionalString(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

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
