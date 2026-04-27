using System.Diagnostics;
using System.Text.Json;
using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Repositories;
using Atlas.Application.Microflows.Services;
using Atlas.Application.Microflows.Runtime.Calls;
using Atlas.Application.Microflows.Runtime.Expressions;
using Atlas.Domain.Microflows.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Application.Microflows.Runtime.Actions;

public sealed class CallMicroflowActionExecutor : IMicroflowActionExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IMicroflowResourceRepository _resourceRepository;
    private readonly IMicroflowSchemaSnapshotRepository _schemaSnapshotRepository;
    private readonly IMicroflowVersionRepository _versionRepository;
    private readonly IMicroflowPublishSnapshotRepository _publishSnapshotRepository;
    private readonly IMicroflowExecutionPlanLoader _executionPlanLoader;
    private readonly IMicroflowCallStackService _callStackService;
    private readonly IServiceProvider _serviceProvider;

    public CallMicroflowActionExecutor(
        IMicroflowResourceRepository resourceRepository,
        IMicroflowSchemaSnapshotRepository schemaSnapshotRepository,
        IMicroflowVersionRepository versionRepository,
        IMicroflowPublishSnapshotRepository publishSnapshotRepository,
        IMicroflowExecutionPlanLoader executionPlanLoader,
        IMicroflowCallStackService callStackService,
        IServiceProvider serviceProvider)
    {
        _resourceRepository = resourceRepository;
        _schemaSnapshotRepository = schemaSnapshotRepository;
        _versionRepository = versionRepository;
        _publishSnapshotRepository = publishSnapshotRepository;
        _executionPlanLoader = executionPlanLoader;
        _callStackService = callStackService;
        _serviceProvider = serviceProvider;
    }

    public string ActionKind => "callMicroflow";

    public string Category => MicroflowActionRuntimeCategory.ServerExecutable;

    public string SupportLevel => MicroflowActionSupportLevel.Supported;

    public async Task<MicroflowActionExecutionResult> ExecuteAsync(MicroflowActionExecutionContext context, CancellationToken ct)
    {
        var started = Stopwatch.StartNew();
        var startedAt = DateTimeOffset.UtcNow;
        var diagnostics = new List<MicroflowCallDiagnostic>();
        var logs = new List<MicroflowRuntimeLogDto>();
        var targetId = ReadString(context.ActionConfig, "targetMicroflowId");
        var targetQualifiedName = ReadString(context.ActionConfig, "targetMicroflowQualifiedName");
        var transactionBoundary = NormalizeTransactionBoundary(ReadString(context.ActionConfig, "transactionBoundary"));

        if (string.IsNullOrWhiteSpace(targetId) && string.IsNullOrWhiteSpace(targetQualifiedName))
        {
            return Failed(
                context,
                started,
                RuntimeErrorCode.RuntimeCallMicroflowFailed,
                "CallMicroflow targetMicroflowId 或 targetMicroflowQualifiedName 必填。",
                diagnostics,
                logs,
                Diagnostic("CALL_TARGET_MISSING", "error", "Call target is missing.", "action.targetMicroflowId"));
        }

        var target = await ResolveTargetAsync(context, targetId, targetQualifiedName, ct);
        if (target is null)
        {
            return Failed(
                context,
                started,
                RuntimeErrorCode.RuntimeCallMicroflowFailed,
                $"CallMicroflow target not found: {targetId ?? targetQualifiedName}.",
                diagnostics,
                logs,
                Diagnostic("CALL_TARGET_NOT_FOUND", "error", $"Call target not found: {targetId ?? targetQualifiedName}.", "action.targetMicroflowId"));
        }

        targetQualifiedName ??= BuildQualifiedName(target);
        if (target.Archived || string.Equals(target.Status, "archived", StringComparison.OrdinalIgnoreCase))
        {
            var archivedDiagnostic = Diagnostic("CALL_TARGET_ARCHIVED", "warning", $"Call target is archived: {targetQualifiedName}.", "action.targetMicroflowId");
            if (string.Equals(context.Options.Mode, MicroflowRuntimeExecutionMode.PublishedRun, StringComparison.OrdinalIgnoreCase))
            {
                return Failed(context, started, RuntimeErrorCode.RuntimeCallMicroflowFailed, archivedDiagnostic.Message, diagnostics, logs, archivedDiagnostic);
            }

            diagnostics.Add(archivedDiagnostic);
        }

        var allowRecursion = ReadBool(context.ActionConfig, "allowRecursion");
        var enter = _callStackService.TryEnter(
            context.RuntimeExecutionContext,
            target.Id,
            targetQualifiedName,
            context.ObjectId,
            context.ActionId,
            context.Options.MaxCallDepth <= 0 ? context.RuntimeExecutionContext.MaxCallDepth : context.Options.MaxCallDepth,
            allowRecursion,
            startedAt);
        if (!enter.Allowed || enter.Frame is null)
        {
            return new MicroflowActionExecutionResult
            {
                Status = MicroflowActionExecutionStatus.Failed,
                Error = enter.Error,
                Diagnostics = enter.Diagnostics.Select(ToActionDiagnostic).ToArray(),
                OutputJson = JsonSerializer.SerializeToElement(new
                {
                    callMicroflow = new
                    {
                        targetResourceId = target.Id,
                        targetQualifiedName,
                        childStatus = enter.Frame?.Status,
                        diagnostics = enter.Diagnostics
                    }
                }, JsonOptions),
                OutputPreview = enter.Error?.Message,
                Logs =
                [
                    Log("error", context, enter.Error?.Message ?? "CallMicroflow recursion/depth guard failed.")
                ],
                ShouldContinueNormalFlow = false,
                ShouldEnterErrorHandler = true,
                ShouldStopRun = true,
                DurationMs = (int)started.ElapsedMilliseconds,
                Message = enter.Error?.Message
            };
        }

        var frame = enter.Frame;
        logs.Add(Log("info", context, $"CallMicroflow enter target={targetQualifiedName} depth={frame.Depth}."));
        using var frameLease = _callStackService.Activate(context.RuntimeExecutionContext, frame);

        SelectedTargetSchema selected;
        try
        {
            selected = await SelectTargetSchemaAsync(target, context, ct);
            frame.TargetSchemaId = selected.SchemaId;
            frame.TargetVersion = selected.Version;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var error = Error(RuntimeErrorCode.RuntimeCallMicroflowFailed, $"CallMicroflow target schema load failed: {ex.Message}", context);
            _callStackService.Complete(frame, MicroflowCallStackFrameStatus.Failed, DateTimeOffset.UtcNow, error);
            return FailureResult(context, started, frame, diagnostics, logs, error);
        }

        var childPlan = await _executionPlanLoader.LoadFromSchemaAsync(
            selected.Schema,
            new MicroflowExecutionPlanLoadOptions
            {
                ResourceId = target.Id,
                Version = selected.Version,
                Mode = MapPlanMode(context.Options.Mode),
                IncludeDiagnostics = true,
                FailOnUnsupported = false,
                WorkspaceId = target.WorkspaceId,
                TenantId = target.TenantId,
                UserId = context.RuntimeExecutionContext.SecurityContext.UserId,
                ConnectorCapabilities = context.Options.ConnectorCapabilities
            },
            ct);

        var bindings = BindParameters(context, childPlan, diagnostics);
        frame.ParameterBindings = bindings.Bindings;
        if (bindings.Error is not null)
        {
            _callStackService.Complete(frame, MicroflowCallStackFrameStatus.Failed, DateTimeOffset.UtcNow, bindings.Error);
            logs.Add(Log("error", context, $"CallMicroflow parameter binding failed: {bindings.Error.Message}"));
            return FailureResult(context, started, frame, diagnostics, logs, bindings.Error);
        }

        MicroflowRunSessionDto childSession;
        try
        {
            var runner = _serviceProvider.GetRequiredService<IMicroflowMockRuntimeRunner>();
            childSession = await runner.RunAsync(
                new MicroflowMockRuntimeRequest
                {
                    ResourceId = target.Id,
                    SchemaId = selected.SchemaId,
                    Version = selected.Version ?? target.Version,
                    Schema = selected.Schema,
                    ExecutionPlan = childPlan,
                    Input = bindings.Input,
                    Options = new MicroflowTestRunOptionsDto(),
                    Metadata = context.MetadataCatalog ?? context.RuntimeExecutionContext.MetadataCatalog,
                    RequestContext = context.RuntimeExecutionContext.SecurityContext,
                    ParentRuntimeContext = context.RuntimeExecutionContext,
                    CallFrame = frame,
                    TransactionBoundary = transactionBoundary,
                    MaxCallDepth = context.Options.MaxCallDepth
                },
                ct);
        }
        catch (OperationCanceledException)
        {
            var error = Error(RuntimeErrorCode.RuntimeCancelled, "CallMicroflow child execution cancelled.", context);
            _callStackService.Complete(frame, MicroflowCallStackFrameStatus.Cancelled, DateTimeOffset.UtcNow, error);
            return FailureResult(context, started, frame, diagnostics, logs, error);
        }

        frame.ChildRunId = childSession.Id;
        frame.ChildTraceRootFrameId = childSession.Trace.FirstOrDefault()?.Id;
        if (!string.Equals(childSession.Status, "success", StringComparison.OrdinalIgnoreCase))
        {
            var error = Error(
                RuntimeErrorCode.RuntimeCallMicroflowFailed,
                $"Child microflow failed: {childSession.Error?.Message ?? childSession.Status}.",
                context,
                cause: childSession.Error is null ? null : JsonSerializer.Serialize(childSession.Error, JsonOptions));
            _callStackService.Complete(frame, MicroflowCallStackFrameStatus.Failed, DateTimeOffset.UtcNow, error);
            logs.Add(Log("error", context, $"CallMicroflow failed childRunId={childSession.Id}."));
            return FailureResult(context, started, frame, diagnostics, logs, error, childSession);
        }

        var returnBinding = BindReturn(context, selected.ReturnTypeJson, childSession, diagnostics);
        frame.ReturnBinding = returnBinding;
        if (returnBinding.Diagnostics.Any(item => string.Equals(item.Severity, "error", StringComparison.OrdinalIgnoreCase)))
        {
            var error = Error(RuntimeErrorCode.RuntimeVariableTypeMismatch, returnBinding.Diagnostics.First(item => item.Severity == "error").Message, context);
            _callStackService.Complete(frame, MicroflowCallStackFrameStatus.Failed, DateTimeOffset.UtcNow, error);
            return FailureResult(context, started, frame, diagnostics, logs, error, childSession);
        }

        _callStackService.Complete(frame, MicroflowCallStackFrameStatus.Success, DateTimeOffset.UtcNow);
        logs.Add(Log("info", context, $"CallMicroflow exit target={targetQualifiedName} childRunId={childSession.Id}."));
        started.Stop();

        var output = BuildOutput(frame, selected, bindings.Bindings, returnBinding, childSession, transactionBoundary, diagnostics);
        return new MicroflowActionExecutionResult
        {
            Status = MicroflowActionExecutionStatus.Success,
            OutputJson = output,
            OutputPreview = $"CallMicroflow {targetQualifiedName} -> {childSession.Status}",
            Logs = logs,
            Diagnostics = diagnostics.Select(ToActionDiagnostic).ToArray(),
            ChildRunSessions = [childSession],
            DurationMs = (int)started.ElapsedMilliseconds
        };
    }

    private async Task<MicroflowResourceEntity?> ResolveTargetAsync(MicroflowActionExecutionContext context, string? targetId, string? targetQualifiedName, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(targetId))
        {
            var byId = await _resourceRepository.GetByIdAsync(targetId!, ct);
            if (byId is not null)
            {
                return byId;
            }
        }

        return string.IsNullOrWhiteSpace(targetQualifiedName)
            ? null
            : await _resourceRepository.GetByQualifiedNameAsync(context.RuntimeExecutionContext.SecurityContext.WorkspaceId, targetQualifiedName!, ct);
    }

    private async Task<SelectedTargetSchema> SelectTargetSchemaAsync(MicroflowResourceEntity target, MicroflowActionExecutionContext context, CancellationToken ct)
    {
        var targetVersion = ReadString(context.ActionConfig, "targetVersion") ?? ReadString(context.ActionConfig, "version");
        if (!string.IsNullOrWhiteSpace(targetVersion))
        {
            var version = await _versionRepository.GetByIdAsync(targetVersion!, ct)
                ?? await _versionRepository.GetByResourceVersionAsync(target.Id, targetVersion!, ct);
            if (version is null || !string.Equals(version.ResourceId, target.Id, StringComparison.Ordinal))
            {
                var published = await _publishSnapshotRepository.GetByResourceVersionAsync(target.Id, targetVersion!, ct)
                    ?? throw new InvalidOperationException($"target version not found: {targetVersion}");
                return new SelectedTargetSchema(published.SchemaSnapshotId, published.Version, MicroflowSchemaJsonHelper.ParseRequired(published.SchemaJson), ReadReturnType(published.SchemaJson), "publishedVersion");
            }

            var snapshot = await _schemaSnapshotRepository.GetByIdAsync(version.SchemaSnapshotId, ct)
                ?? throw new InvalidOperationException($"target version snapshot not found: {version.SchemaSnapshotId}");
            return new SelectedTargetSchema(snapshot.Id, version.Version, MicroflowSchemaJsonHelper.ParseRequired(snapshot.SchemaJson), ReadReturnType(snapshot.SchemaJson), "version");
        }

        var preferPublished = ReadBoolByPath(context.ActionConfig, "options", "preferPublishedTargets");
        if (string.Equals(context.Options.Mode, MicroflowRuntimeExecutionMode.PublishedRun, StringComparison.OrdinalIgnoreCase) || preferPublished)
        {
            var published = await _publishSnapshotRepository.GetLatestByResourceIdAsync(target.Id, ct);
            if (published is null)
            {
                if (!ReadBoolByPath(context.ActionConfig, "options", "allowDraftTargetInPublishedRun"))
                {
                    throw new InvalidOperationException("latest published target snapshot not found");
                }
            }
            else
            {
                return new SelectedTargetSchema(published.SchemaSnapshotId, published.Version, MicroflowSchemaJsonHelper.ParseRequired(published.SchemaJson), ReadReturnType(published.SchemaJson), "latestPublished");
            }
        }

        var current = !string.IsNullOrWhiteSpace(target.CurrentSchemaSnapshotId)
            ? await _schemaSnapshotRepository.GetByIdAsync(target.CurrentSchemaSnapshotId!, ct)
            : null;
        current ??= !string.IsNullOrWhiteSpace(target.SchemaId)
            ? await _schemaSnapshotRepository.GetByIdAsync(target.SchemaId!, ct)
            : null;
        current ??= await _schemaSnapshotRepository.GetLatestByResourceIdAsync(target.Id, ct);
        if (current is null)
        {
            throw new InvalidOperationException("current target schema snapshot not found");
        }

        return new SelectedTargetSchema(current.Id, target.Version, MicroflowSchemaJsonHelper.ParseRequired(current.SchemaJson), ReadReturnType(current.SchemaJson), "current");
    }

    private ParameterBindingResult BindParameters(MicroflowActionExecutionContext context, MicroflowExecutionPlan childPlan, List<MicroflowCallDiagnostic> diagnostics)
    {
        var mappings = ReadParameterMappings(context.ActionConfig);
        var input = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var bindings = new List<MicroflowCallParameterBinding>();
        foreach (var parameter in childPlan.Parameters)
        {
            var mapping = mappings.FirstOrDefault(item => string.Equals(item.ParameterName, parameter.Name, StringComparison.Ordinal));
            if (mapping is null)
            {
                if (parameter.Required)
                {
                    var diagnostic = Diagnostic("CALL_PARAMETER_REQUIRED_MISSING", "error", $"Required parameter '{parameter.Name}' is missing.", "action.parameterMappings");
                    diagnostics.Add(diagnostic);
                    bindings.Add(new MicroflowCallParameterBinding
                    {
                        ParameterName = parameter.Name,
                        ParameterTypeJson = parameter.DataTypeJson.GetRawText(),
                        Status = "failed",
                        Diagnostics = [diagnostic]
                    });
                    return new ParameterBindingResult(bindings, input, Error(RuntimeErrorCode.RuntimeCallMicroflowFailed, diagnostic.Message, context));
                }

                continue;
            }

            var expected = MicroflowExpressionTypeHelper.FromDataType(parameter.DataTypeJson, parameter.DataTypeJson.GetRawText());
            var evaluated = context.ExpressionEvaluator?.Evaluate(
                mapping.ArgumentExpression ?? "null",
                new MicroflowExpressionEvaluationContext
                {
                    RuntimeExecutionContext = context.RuntimeExecutionContext,
                    VariableStore = context.VariableStore,
                    MetadataCatalog = context.MetadataCatalog ?? context.RuntimeExecutionContext.MetadataCatalog,
                    MetadataResolver = context.MetadataResolver,
                    CurrentObjectId = context.ObjectId,
                    CurrentActionId = context.ActionId,
                    CurrentCollectionId = context.CollectionId,
                    ExpectedType = expected,
                    Mode = context.Options.Mode,
                    Options = new MicroflowExpressionEvaluationOptions
                    {
                        AllowUnknownVariables = false,
                        AllowUnsupportedFunctions = false,
                        StrictTypeCheck = true,
                        MaxEvaluationDepth = 64,
                        MaxStringLength = 500
                    }
                });

            if (evaluated is null || !evaluated.Success || evaluated.Value is null)
            {
                var diagnostic = Diagnostic("CALL_PARAMETER_EXPRESSION_FAILED", "error", $"Parameter '{parameter.Name}' expression failed.", "action.parameterMappings");
                diagnostics.Add(diagnostic);
                bindings.Add(new MicroflowCallParameterBinding
                {
                    ParameterName = parameter.Name,
                    ParameterTypeJson = parameter.DataTypeJson.GetRawText(),
                    ArgumentExpression = mapping.ArgumentExpression,
                    Status = "failed",
                    Diagnostics = [diagnostic]
                });
                return new ParameterBindingResult(bindings, input, Error(RuntimeErrorCode.RuntimeExpressionError, diagnostic.Message, context));
            }

            input[parameter.Name] = MicroflowVariableStore.ToJsonElement(evaluated.Value.RawValueJson)
                ?? JsonSerializer.SerializeToElement(evaluated.Value.ValuePreview, JsonOptions);
            bindings.Add(new MicroflowCallParameterBinding
            {
                ParameterName = parameter.Name,
                ParameterTypeJson = parameter.DataTypeJson.GetRawText(),
                ArgumentExpression = mapping.ArgumentExpression,
                ValueJson = evaluated.Value.RawValueJson,
                ValuePreview = evaluated.ValuePreview,
                Status = "success"
            });
        }

        foreach (var extra in mappings.Where(mapping => childPlan.Parameters.All(parameter => !string.Equals(parameter.Name, mapping.ParameterName, StringComparison.Ordinal))))
        {
            var diagnostic = Diagnostic("CALL_PARAMETER_UNKNOWN", "error", $"Parameter mapping targets unknown parameter '{extra.ParameterName}'.", "action.parameterMappings");
            diagnostics.Add(diagnostic);
            bindings.Add(new MicroflowCallParameterBinding
            {
                ParameterName = extra.ParameterName,
                ArgumentExpression = extra.ArgumentExpression,
                Status = "failed",
                Diagnostics = [diagnostic]
            });
            return new ParameterBindingResult(bindings, input, Error(RuntimeErrorCode.RuntimeCallMicroflowFailed, diagnostic.Message, context));
        }

        return new ParameterBindingResult(bindings, input, null);
    }

    private MicroflowCallReturnBinding BindReturn(MicroflowActionExecutionContext context, string returnTypeJson, MicroflowRunSessionDto childSession, List<MicroflowCallDiagnostic> diagnostics)
    {
        var storeResult = ReadBoolByPath(context.ActionConfig, "returnValue", "storeResult")
            || !string.IsNullOrWhiteSpace(ReadStringByPath(context.ActionConfig, "returnValue", "outputVariableName"))
            || !string.IsNullOrWhiteSpace(ReadString(context.ActionConfig, "outputVariableName"));
        var outputVariableName = ReadStringByPath(context.ActionConfig, "returnValue", "outputVariableName")
            ?? ReadString(context.ActionConfig, "outputVariableName");
        var returnKind = ReadTypeKind(returnTypeJson);
        var localDiagnostics = new List<MicroflowCallDiagnostic>();
        var childValue = ExtractChildReturnValue(childSession.Output, out var valuePreview);
        var valueJson = childValue.HasValue ? childValue.Value.GetRawText() : null;

        if (string.Equals(returnKind, "void", StringComparison.OrdinalIgnoreCase) && storeResult)
        {
            localDiagnostics.Add(Diagnostic("CALL_RETURN_VOID_STORE_RESULT", "error", "Void target cannot store a return value.", "action.returnValue.storeResult"));
        }

        if (!string.Equals(returnKind, "void", StringComparison.OrdinalIgnoreCase) && !childValue.HasValue)
        {
            localDiagnostics.Add(Diagnostic("CALL_RETURN_VALUE_MISSING", "error", "Child microflow did not return a value.", "action.returnValue"));
        }

        if (storeResult && string.IsNullOrWhiteSpace(outputVariableName))
        {
            localDiagnostics.Add(Diagnostic("CALL_RETURN_OUTPUT_VARIABLE_MISSING", "error", "outputVariableName is required when storeResult=true.", "action.returnValue.outputVariableName"));
        }

        if (storeResult && !string.IsNullOrWhiteSpace(outputVariableName) && context.VariableStore.Exists(outputVariableName!))
        {
            localDiagnostics.Add(Diagnostic("CALL_RETURN_OUTPUT_VARIABLE_DUPLICATED", "error", $"Variable '{outputVariableName}' already exists.", "action.returnValue.outputVariableName"));
        }

        if (localDiagnostics.Count == 0 && storeResult && !string.IsNullOrWhiteSpace(outputVariableName) && childValue.HasValue)
        {
            context.VariableStore.Define(new MicroflowVariableDefinition
            {
                Name = outputVariableName!,
                DataTypeJson = returnTypeJson,
                RawValueJson = childValue.Value.GetRawText(),
                ValuePreview = valuePreview ?? MicroflowVariableStore.Preview(childValue.Value.GetRawText()),
                SourceKind = MicroflowVariableSourceKind.MicroflowReturn,
                SourceObjectId = context.ObjectId,
                SourceActionId = context.ActionId,
                CollectionId = context.CollectionId,
                ScopeKind = MicroflowVariableScopeKind.Action
            });
        }

        diagnostics.AddRange(localDiagnostics);
        return new MicroflowCallReturnBinding
        {
            StoreResult = storeResult,
            OutputVariableName = outputVariableName,
            ReturnTypeJson = returnTypeJson,
            ValueJson = valueJson,
            ValuePreview = valuePreview,
            Status = localDiagnostics.Any(item => item.Severity == "error") ? "failed" : "success",
            Diagnostics = localDiagnostics
        };
    }

    private MicroflowActionExecutionResult FailureResult(
        MicroflowActionExecutionContext context,
        Stopwatch started,
        MicroflowCallStackFrame frame,
        List<MicroflowCallDiagnostic> diagnostics,
        List<MicroflowRuntimeLogDto> logs,
        MicroflowRuntimeErrorDto error,
        MicroflowRunSessionDto? childSession = null)
    {
        started.Stop();
        return new MicroflowActionExecutionResult
        {
            Status = MicroflowActionExecutionStatus.Failed,
            Error = error,
            OutputJson = BuildOutput(frame, null, frame.ParameterBindings, frame.ReturnBinding, childSession, NormalizeTransactionBoundary(ReadString(context.ActionConfig, "transactionBoundary")), diagnostics),
            OutputPreview = error.Message,
            Diagnostics = diagnostics.Concat(frame.Diagnostics).Select(ToActionDiagnostic).ToArray(),
            Logs = logs,
            ChildRunSessions = childSession is null ? Array.Empty<MicroflowRunSessionDto>() : [childSession],
            ShouldContinueNormalFlow = false,
            ShouldEnterErrorHandler = true,
            ShouldStopRun = true,
            DurationMs = (int)started.ElapsedMilliseconds,
            Message = error.Message
        };
    }

    private MicroflowActionExecutionResult Failed(
        MicroflowActionExecutionContext context,
        Stopwatch started,
        string code,
        string message,
        List<MicroflowCallDiagnostic> diagnostics,
        List<MicroflowRuntimeLogDto> logs,
        MicroflowCallDiagnostic diagnostic)
    {
        diagnostics.Add(diagnostic);
        var error = Error(code, message, context);
        started.Stop();
        return new MicroflowActionExecutionResult
        {
            Status = MicroflowActionExecutionStatus.Failed,
            Error = error,
            OutputJson = JsonSerializer.SerializeToElement(new { callMicroflow = new { diagnostics } }, JsonOptions),
            OutputPreview = message,
            Diagnostics = diagnostics.Select(ToActionDiagnostic).ToArray(),
            Logs = logs,
            ShouldContinueNormalFlow = false,
            ShouldEnterErrorHandler = true,
            ShouldStopRun = true,
            DurationMs = (int)started.ElapsedMilliseconds,
            Message = message
        };
    }

    private static JsonElement BuildOutput(
        MicroflowCallStackFrame frame,
        SelectedTargetSchema? selected,
        IReadOnlyList<MicroflowCallParameterBinding> parameterBindings,
        MicroflowCallReturnBinding? returnBinding,
        MicroflowRunSessionDto? childSession,
        string transactionBoundary,
        IReadOnlyList<MicroflowCallDiagnostic> diagnostics)
        => JsonSerializer.SerializeToElement(new
        {
            callMicroflow = new
            {
                targetResourceId = frame.TargetResourceId,
                targetQualifiedName = frame.TargetQualifiedName,
                targetVersion = frame.TargetVersion ?? selected?.Version,
                targetSchemaId = frame.TargetSchemaId ?? selected?.SchemaId,
                schemaSelection = selected?.Selection,
                callFrameId = frame.FrameId,
                callDepth = frame.Depth,
                transactionBoundary,
                parameterBindings,
                returnBinding,
                childRunId = childSession?.Id ?? frame.ChildRunId,
                childStatus = childSession?.Status ?? frame.Status,
                childTraceSummary = childSession is null ? null : new
                {
                    traceFrameCount = childSession.Trace.Count,
                    rootFrameId = childSession.Trace.FirstOrDefault()?.Id,
                    error = childSession.Error
                },
                durationMs = frame.DurationMs,
                diagnostics
            }
        }, JsonOptions);

    private static JsonElement? ExtractChildReturnValue(JsonElement? output, out string? preview)
    {
        preview = null;
        if (!output.HasValue || output.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (output.Value.TryGetProperty("valuePreview", out var previewElement) && previewElement.ValueKind == JsonValueKind.String)
        {
            preview = previewElement.GetString();
        }

        if (!output.Value.TryGetProperty("returnValue", out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            var raw = value.GetString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return JsonSerializer.SerializeToElement(raw, JsonOptions);
            }

            try
            {
                using var doc = JsonDocument.Parse(raw);
                return doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                return JsonSerializer.SerializeToElement(raw, JsonOptions);
            }
        }

        return value.Clone();
    }

    private static IReadOnlyList<ParameterMapping> ReadParameterMappings(JsonElement config)
    {
        if (!config.TryGetProperty("parameterMappings", out var mappings) || mappings.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<ParameterMapping>();
        }

        return mappings.EnumerateArray()
            .Select(item => new ParameterMapping(
                ReadString(item, "parameterName") ?? string.Empty,
                ReadExpressionText(item, "argumentExpression")
                    ?? ReadExpressionText(item, "expression")
                    ?? ReadExpressionText(item, "valueExpression")
                    ?? ReadString(item, "value")))
            .Where(item => !string.IsNullOrWhiteSpace(item.ParameterName))
            .ToArray();
    }

    private static string ReadReturnType(string schemaJson)
    {
        using var document = JsonDocument.Parse(schemaJson);
        return document.RootElement.TryGetProperty("returnType", out var returnType)
            ? returnType.GetRawText()
            : JsonSerializer.Serialize(new { kind = "void" }, JsonOptions);
    }

    private static string ReadTypeKind(string dataTypeJson)
    {
        try
        {
            using var document = JsonDocument.Parse(dataTypeJson);
            return ReadString(document.RootElement, "kind") ?? "unknown";
        }
        catch (JsonException)
        {
            return "unknown";
        }
    }

    private static MicroflowRuntimeErrorDto Error(string code, string message, MicroflowActionExecutionContext context, string? cause = null)
        => new()
        {
            Code = code,
            Message = message,
            ObjectId = context.ObjectId,
            ActionId = context.ActionId,
            Cause = cause
        };

    private static MicroflowCallDiagnostic Diagnostic(string code, string severity, string message, string? fieldPath = null)
        => new() { Code = code, Severity = severity, Message = message, FieldPath = fieldPath };

    private static MicroflowActionExecutionDiagnostic ToActionDiagnostic(MicroflowCallDiagnostic diagnostic)
        => new()
        {
            Code = diagnostic.Code,
            Severity = diagnostic.Severity,
            Message = diagnostic.Message,
            ActionKind = "callMicroflow"
        };

    private static MicroflowRuntimeLogDto Log(string level, MicroflowActionExecutionContext context, string message)
        => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Timestamp = DateTimeOffset.UtcNow,
            Level = level,
            ObjectId = context.ObjectId,
            ActionId = context.ActionId,
            Message = message
        };

    private static string MapPlanMode(string mode)
        => mode switch
        {
            MicroflowRuntimeExecutionMode.PublishedRun => MicroflowExecutionPlanMode.PublishedRun,
            MicroflowRuntimeExecutionMode.PreviewRun => MicroflowExecutionPlanMode.PreviewRun,
            _ => MicroflowExecutionPlanMode.TestRun
        };

    private static string NormalizeTransactionBoundary(string? value)
        => value switch
        {
            MicroflowCallTransactionBoundary.SharedTransaction => MicroflowCallTransactionBoundary.SharedTransaction,
            MicroflowCallTransactionBoundary.ChildTransaction => MicroflowCallTransactionBoundary.ChildTransaction,
            MicroflowCallTransactionBoundary.NoTransaction => MicroflowCallTransactionBoundary.NoTransaction,
            _ => MicroflowCallTransactionBoundary.Inherit
        };

    private static string BuildQualifiedName(MicroflowResourceEntity resource)
    {
        var moduleName = string.IsNullOrWhiteSpace(resource.ModuleName) ? resource.ModuleId : resource.ModuleName!;
        return $"{moduleName}.{resource.Name}";
    }

    private static string? ReadString(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

    private static string? ReadStringByPath(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }

    private static bool ReadBool(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.True;

    private static bool ReadBoolByPath(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return false;
            }
        }

        return current.ValueKind == JsonValueKind.True;
    }

    private static string? ReadExpressionText(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            return value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : value.GetRawText();
        }

        return ReadString(value, "raw") ?? ReadString(value, "text") ?? ReadString(value, "expression");
    }

    private sealed record SelectedTargetSchema(string SchemaId, string? Version, JsonElement Schema, string ReturnTypeJson, string Selection);

    private sealed record ParameterMapping(string ParameterName, string? ArgumentExpression);

    private sealed record ParameterBindingResult(
        IReadOnlyList<MicroflowCallParameterBinding> Bindings,
        IReadOnlyDictionary<string, JsonElement> Input,
        MicroflowRuntimeErrorDto? Error);
}
