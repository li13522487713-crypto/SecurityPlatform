using System.Globalization;
using System.Text.Json;
using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Repositories;
using Atlas.Application.Microflows.Runtime.Actions;
using Atlas.Application.Microflows.Runtime.Branches;
using Atlas.Application.Microflows.Runtime.Calls;
using Atlas.Application.Microflows.Runtime.Debug;
using Atlas.Application.Microflows.Runtime.ErrorHandling;
using Atlas.Application.Microflows.Runtime.Expressions;
using Atlas.Application.Microflows.Runtime.Loops;
using Atlas.Application.Microflows.Runtime.Security;
using Atlas.Application.Microflows.Runtime.Transactions;
using Atlas.Application.Microflows.Services;

namespace Atlas.Application.Microflows.Runtime;

public interface IMicroflowRuntimeEngine
{
    Task<MicroflowRunSessionDto> RunAsync(MicroflowExecutionRequest request, CancellationToken cancellationToken);
}

public sealed class MicroflowRuntimeEngine : IMicroflowRuntimeEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IMicroflowSchemaReader _schemaReader;
    private readonly IMicroflowClock _clock;
    private readonly IMicroflowExpressionEvaluator _expressionEvaluator;
    private readonly IMicroflowResourceRepository? _resourceRepository;
    private readonly IMicroflowSchemaSnapshotRepository? _schemaSnapshotRepository;
    // Registry / connector are optional. When DI is wired they enable real action
    // dispatch (retrieve / createObject / restCall / logMessage / aggregateList ...).
    // Legacy unit tests construct the engine without them; in that case the engine
    // keeps the previous fast-path-only behaviour and reports unsupported actions.
    private readonly IMicroflowActionExecutorRegistry? _actionExecutorRegistry;
    private readonly IMicroflowLoopExecutor? _loopExecutor;
    private readonly IMicroflowRuntimeConnectorRegistry? _connectorRegistry;
    private readonly IMicroflowErrorHandlingService? _errorHandlingService;
    private readonly IMicroflowDebugCoordinator? _debugCoordinator;
    private readonly IMicroflowRunCancellationRegistry? _cancellationRegistry;
    private readonly MicroflowEntityAccessOptions _runtimeOptions;
    private readonly IMicroflowTransactionManager? _transactionManager;
    private readonly IMicroflowRuntimeDbSessionFactory? _runtimeDbSessionFactory;
    private readonly IVariableScopeForker _variableScopeForker;
    private readonly IBranchMergePolicy _branchMergePolicy;

    public MicroflowRuntimeEngine(IMicroflowSchemaReader schemaReader, IMicroflowClock clock)
        : this(schemaReader, clock, new MicroflowExpressionEvaluator(), null, null, null, null, null, null, null)
    {
    }

    public MicroflowRuntimeEngine(
        IMicroflowSchemaReader schemaReader,
        IMicroflowClock clock,
        IMicroflowResourceRepository? resourceRepository,
        IMicroflowSchemaSnapshotRepository? schemaSnapshotRepository)
        : this(schemaReader, clock, new MicroflowExpressionEvaluator(), resourceRepository, schemaSnapshotRepository, null, null, null, null, null)
    {
    }

    public MicroflowRuntimeEngine(
        IMicroflowSchemaReader schemaReader,
        IMicroflowClock clock,
        IMicroflowExpressionEvaluator expressionEvaluator,
        IMicroflowResourceRepository? resourceRepository,
        IMicroflowSchemaSnapshotRepository? schemaSnapshotRepository)
        : this(schemaReader, clock, expressionEvaluator, resourceRepository, schemaSnapshotRepository, null, null, null, null, null)
    {
    }

    public MicroflowRuntimeEngine(
        IMicroflowSchemaReader schemaReader,
        IMicroflowClock clock,
        IMicroflowExpressionEvaluator expressionEvaluator,
        IMicroflowResourceRepository? resourceRepository,
        IMicroflowSchemaSnapshotRepository? schemaSnapshotRepository,
        IMicroflowActionExecutorRegistry? actionExecutorRegistry,
        IMicroflowLoopExecutor? loopExecutor,
        IMicroflowRuntimeConnectorRegistry? connectorRegistry,
        IMicroflowErrorHandlingService? errorHandlingService,
        IMicroflowDebugCoordinator? debugCoordinator = null,
        IMicroflowRunCancellationRegistry? cancellationRegistry = null,
        MicroflowEntityAccessOptions? runtimeOptions = null,
        IMicroflowTransactionManager? transactionManager = null,
        IMicroflowRuntimeDbSessionFactory? runtimeDbSessionFactory = null,
        IVariableScopeForker? variableScopeForker = null,
        IBranchMergePolicy? branchMergePolicy = null)
    {
        _schemaReader = schemaReader;
        _clock = clock;
        _expressionEvaluator = expressionEvaluator;
        _resourceRepository = resourceRepository;
        _schemaSnapshotRepository = schemaSnapshotRepository;
        _actionExecutorRegistry = actionExecutorRegistry;
        _loopExecutor = loopExecutor;
        _connectorRegistry = connectorRegistry;
        _errorHandlingService = errorHandlingService;
        _debugCoordinator = debugCoordinator;
        _cancellationRegistry = cancellationRegistry;
        _runtimeOptions = runtimeOptions ?? new MicroflowEntityAccessOptions();
        _transactionManager = transactionManager;
        _runtimeDbSessionFactory = runtimeDbSessionFactory;
        _variableScopeForker = variableScopeForker ?? new DefaultVariableScopeForker();
        _branchMergePolicy = branchMergePolicy ?? new NoOpBranchMergePolicy();
    }

    public Task<MicroflowRunSessionDto> RunAsync(MicroflowExecutionRequest request, CancellationToken cancellationToken)
    {
        var state = new CallExecutionState();
        return RunInternalAsync(request, state, parent: null, cancellationToken);
    }

    private async Task<MicroflowRunSessionDto> RunInternalAsync(
        MicroflowExecutionRequest request,
        CallExecutionState state,
        ParentCallContext? parent,
        CancellationToken cancellationToken)
    {
        var startedAt = _clock.UtcNow;
        var runId = parent?.ChildRunId ?? request.RequestContext.TraceId;
        if (string.IsNullOrWhiteSpace(runId))
        {
            runId = Guid.NewGuid().ToString("N");
        }

        var rootRunId = parent?.RootRunId ?? runId!;
        var context = new RuntimeContext(
            request,
            _schemaReader.Read(request.Schema),
            startedAt,
            _clock,
            _expressionEvaluator,
            _transactionManager,
            _runtimeDbSessionFactory,
            runId!,
            parent?.ParentRunId,
            rootRunId,
            parent?.CorrelationId ?? request.CorrelationId ?? Guid.NewGuid().ToString("N"),
            parent?.CallDepth ?? 0,
            parent?.CallStack ?? [request.ResourceId],
            parent?.CallerObjectId,
            parent?.CallerActionId);
        using var timeoutCts = CreateRunTimeoutCts(parent);
        using var effectiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts?.Token ?? CancellationToken.None);
        var effectiveToken = effectiveCts.Token;
        CancellationTokenSource? registryCts = null;
        if (_cancellationRegistry is not null)
        {
            registryCts = _cancellationRegistry.Register(runId!, effectiveToken);
            effectiveToken = registryCts.Token;
        }
        try
        {
            effectiveToken.ThrowIfCancellationRequested();
            var graph = MicroflowRuntimeGraph.Build(
                context.Model,
                context.ResolveExecutionPlan(),
                context.ResolveExecutionPlanQuery());
            var bindingError = BindParameters(context);
            if (bindingError is not null)
            {
                return context.BuildSession("failed", bindingError);
            }

            var start = graph.FindStart();
            if (!start.Success)
            {
                return context.BuildSession("failed", start.Error);
            }

            var currentNodeId = start.Object!.Id;
            string? incomingFlowId = null;
            while (!string.IsNullOrWhiteSpace(currentNodeId))
            {
                effectiveToken.ThrowIfCancellationRequested();
                if (!context.TryStep(out var stepError))
                {
                    return context.BuildSession("failed", stepError);
                }

                if (!graph.Objects.TryGetValue(currentNodeId, out var node))
                {
                    return context.BuildSession("failed", Error(RuntimeErrorCode.RuntimeObjectNotFound, $"运行对象不存在：{currentNodeId}", currentNodeId, flowId: incomingFlowId));
                }

                await DebugCheckpointAsync(context, node, incomingFlowId, MicroflowDebugPausePhase.BeforeNode, effectiveToken).ConfigureAwait(false);

                var execution = await ExecuteNodeAsync(context, graph, node, incomingFlowId, state, effectiveToken).ConfigureAwait(false);
                if (!execution.Success)
                {
                    return context.BuildSession("failed", execution.Error);
                }

                if (execution.Completed)
                {
                    await DebugCheckpointAsync(context, node, incomingFlowId, MicroflowDebugPausePhase.AfterNode, effectiveToken).ConfigureAwait(false);
                    return context.BuildSession("success", null, execution.Output);
                }

                await DebugCheckpointAsync(context, node, incomingFlowId, MicroflowDebugPausePhase.AfterNode, effectiveToken).ConfigureAwait(false);

                currentNodeId = execution.NextNodeId;
                incomingFlowId = execution.OutgoingFlowId;
            }

            return context.BuildSession("failed", Error(RuntimeErrorCode.RuntimeEndNotReached, "微流未到达 End 节点。"));
        }
        catch (OperationCanceledException) when (timeoutCts?.IsCancellationRequested == true)
        {
            var timeoutError = Error(RuntimeErrorCode.RuntimeTimeout, "微流运行超时。", details: $"runId={runId}; timeoutSeconds={_runtimeOptions.RunTimeoutSeconds}");
            context.AddFrame("$runtime", "Runtime Timeout", "runtime", actionId: null, incomingFlowId: null, outgoingFlowId: null, status: "failed", input: JsonObj(new { runId, rootRunId = context.RootRunId, correlationId = context.CorrelationId }), output: JsonObj(new { code = timeoutError.Code, message = timeoutError.Message }), error: timeoutError, message: "Run timeout triggered.");
            return context.BuildSession("failed", timeoutError);
        }
        catch (OperationCanceledException)
        {
            var cancelledError = Error(RuntimeErrorCode.RuntimeCancelled, "微流运行已取消。", details: $"runId={runId}");
            context.AddFrame("$runtime", "Runtime Cancelled", "runtime", actionId: null, incomingFlowId: null, outgoingFlowId: null, status: "failed", input: JsonObj(new { runId, rootRunId = context.RootRunId, correlationId = context.CorrelationId }), output: JsonObj(new { code = cancelledError.Code, message = cancelledError.Message }), error: cancelledError, message: "Run cancelled.");
            return context.BuildSession("failed", cancelledError);
        }
        catch (RuntimeExpressionException ex)
        {
            return context.BuildSession("failed", ex.Error);
        }
        catch (JsonException ex)
        {
            return context.BuildSession("failed", Error(RuntimeErrorCode.RuntimeUnknownError, "微流 schema 解析失败。", details: ex.Message));
        }
        finally
        {
            _cancellationRegistry?.Unregister(runId!);
            registryCts?.Dispose();
        }
    }

    private CancellationTokenSource? CreateRunTimeoutCts(ParentCallContext? parent)
    {
        if (parent is not null)
        {
            return null;
        }

        var seconds = _runtimeOptions.RunTimeoutSeconds;
        if (seconds <= 0)
        {
            return null;
        }

        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(seconds));
        return cts;
    }

    private static MicroflowRuntimeErrorDto? BindParameters(RuntimeContext context)
    {
        var outputs = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var parameter in context.Model.Parameters)
        {
            if (string.IsNullOrWhiteSpace(parameter.Name))
            {
                return Error(RuntimeErrorCode.RuntimeVariableNotFound, $"参数名称不能为空：{parameter.Id}");
            }

            var required = ReadBool(parameter.Raw, "required");
            JsonElement? source = null;
            if (context.Input.TryGetValue(parameter.Name, out var input))
            {
                source = input;
            }
            else if (TryReadDefaultValue(parameter.Raw, out var defaultValue))
            {
                source = defaultValue;
            }
            else if (required)
            {
                return Error(RuntimeErrorCode.RuntimeVariableNotFound, $"缺少必填参数：{parameter.Name}");
            }

            var converted = ConvertToType(source ?? JsonNull(), parameter.Type, parameter.Name, out var error);
            if (converted is null || error is not null)
            {
                return error ?? Error(RuntimeErrorCode.RuntimeVariableTypeMismatch, $"参数 {parameter.Name} 类型转换失败。");
            }

            context.SetVariable(parameter.Name, parameter.Type, converted.Value, "parameter");
            outputs[parameter.Name] = ToPlainValue(converted.Value);
        }

        if (context.Model.Parameters.Count > 0)
        {
            context.AddFrame(
                "$parameters",
                "Parameter Binding",
                "parameterBinding",
                actionId: null,
                incomingFlowId: null,
                outgoingFlowId: null,
                status: "success",
                input: JsonSerializer.SerializeToElement(context.Input, JsonOptions),
                output: JsonSerializer.SerializeToElement(outputs, JsonOptions),
                error: null,
                message: "Runtime parameters bound.");
        }

        return null;
    }

    private Task DebugCheckpointAsync(
        RuntimeContext context,
        MicroflowObjectModel node,
        string? incomingFlowId,
        MicroflowDebugPausePhase phase,
        CancellationToken cancellationToken)
    {
        if (_debugCoordinator is null || string.IsNullOrWhiteSpace(context.DebugSessionId))
            return Task.CompletedTask;

        var semantic = ResolveDebugSemanticKind(node, incomingFlowId);
        var effectivePhase = ResolveDebugPausePhase(phase, semantic);
        var point = new MicroflowDebugSafePoint(effectivePhase, node.Id, node.Kind, incomingFlowId)
        {
            CallDepth = context.CallDepth,
            CallStackFrameId = context.CallerObjectId is null ? context.RunId : $"{context.RunId}:{context.CallerObjectId}",
            SemanticKind = semantic
        };
        return _debugCoordinator.WaitAtSafePointAsync(
            context.DebugSessionId,
            context.RunId,
            point,
            context.CreateDebugSnapshot(point),
            cancellationToken);
    }

    private static string ResolveDebugSemanticKind(MicroflowObjectModel node, string? incomingFlowId)
    {
        if (node.Kind is "startEvent" or "endEvent" or "exclusiveSplit" or "inclusiveGateway" or "parallelGateway")
        {
            return node.Kind;
        }

        if (!string.IsNullOrWhiteSpace(incomingFlowId) && node.Kind == "actionActivity" && node.Action is not null)
        {
            return node.Action.Kind switch
            {
                "callMicroflow" => "callMicroflow",
                "restCall" or "restOperationCall" => "rest",
                "webServiceCall" => "webservice",
                "callExternalAction" or "deleteExternalObject" or "sendExternalObject" => "external",
                _ => "activity"
            };
        }

        return node.Kind == "actionActivity" ? "activity" : node.Kind;
    }

    private static MicroflowDebugPausePhase ResolveDebugPausePhase(MicroflowDebugPausePhase phase, string semanticKind)
    {
        if (semanticKind == "callMicroflow")
        {
            return phase == MicroflowDebugPausePhase.BeforeNode
                ? MicroflowDebugPausePhase.BeforeCallMicroflow
                : MicroflowDebugPausePhase.AfterCallMicroflow;
        }

        if (semanticKind is "rest" or "webservice" or "external")
        {
            return phase == MicroflowDebugPausePhase.BeforeNode
                ? MicroflowDebugPausePhase.BeforeNode
                : MicroflowDebugPausePhase.AfterNode;
        }

        return phase;
    }

    private async Task<NodeExecution> ExecuteNodeAsync(
        RuntimeContext context,
        MicroflowRuntimeGraph graph,
        MicroflowObjectModel node,
        string? incomingFlowId,
        CallExecutionState state,
        CancellationToken cancellationToken)
    {
        return node.Kind switch
        {
            "startEvent" => ExecuteSingleOutgoing(context, graph, node, incomingFlowId, "Start"),
            "parameterObject" => ExecuteSingleOutgoing(context, graph, node, incomingFlowId, "Parameter"),
            "exclusiveMerge" => ExecuteSingleOutgoing(context, graph, node, incomingFlowId, "Merge"),
            "endEvent" => ExecuteEnd(context, node, incomingFlowId),
            "exclusiveSplit" => ExecuteDecision(context, graph, node, incomingFlowId),
            "inheritanceSplit" => ExecuteObjectTypeDecision(context, graph, node, incomingFlowId),
            "actionActivity" => await ExecuteActionAsync(context, graph, node, incomingFlowId, state, cancellationToken),
            "loopedActivity" => await ExecuteLoopActivityAsync(context, graph, node, incomingFlowId, cancellationToken).ConfigureAwait(false),
            "errorEvent" => ExecuteErrorEvent(context, node, incomingFlowId),
            "annotation" => ExecuteAnnotationPassThrough(context, graph, node, incomingFlowId),
            "parallelGateway" or "parallelSplit" or "parallelMerge"
                => await ExecuteParallelGatewayAsync(context, graph, node, incomingFlowId, state, cancellationToken).ConfigureAwait(false),
            "inclusiveGateway" or "inclusiveSplit" or "inclusiveMerge"
                => await ExecuteInclusiveGatewayAsync(context, graph, node, incomingFlowId, state, cancellationToken).ConfigureAwait(false),
            "tryCatch"
                => ExecuteGatewayPassThrough(context, graph, node, incomingFlowId, "tryCatch"),
            "errorHandler"
                => ExecuteGatewayPassThrough(context, graph, node, incomingFlowId, "errorHandler"),
            _ => UnsupportedNode(context, node, incomingFlowId, $"节点类型 {node.Kind} 不在 runtime 主路径支持的列表中。")
        };
    }

    private static NodeExecution UnsupportedNode(RuntimeContext context, MicroflowObjectModel node, string? incomingFlowId, string message)
    {
        var error = Error(RuntimeErrorCode.RuntimeUnsupportedAction, message, node.Id, flowId: incomingFlowId);
        context.AddNodeFailure(node, incomingFlowId, error);
        return NodeExecution.Failed(error);
    }

    private async Task<NodeExecution> ExecuteLoopActivityAsync(
        RuntimeContext context,
        MicroflowRuntimeGraph graph,
        MicroflowObjectModel node,
        string? incomingFlowId,
        CancellationToken cancellationToken)
    {
        if (_loopExecutor is null || _actionExecutorRegistry is null)
        {
            var executorMissing = Error(RuntimeErrorCode.RuntimeLoopBodyNotFound, "Loop executor 未注册，无法执行 loopedActivity。", node.Id, flowId: incomingFlowId);
            context.AddNodeFailure(node, incomingFlowId, executorMissing);
            return NodeExecution.Failed(executorMissing);
        }

        var actionContext = new MicroflowActionExecutionContext
        {
            RuntimeExecutionContext = context.AsRuntimeExecutionContext(),
            ExecutionPlan = context.ResolveExecutionPlan(),
            ExecutionNode = context.ResolveLoopExecutionNode(node),
            ActionConfig = node.Raw,
            ActionKind = "loopedActivity",
            ObjectId = node.Id,
            CollectionId = node.CollectionId,
            VariableStore = context.VariableStore,
            ExpressionEvaluator = _expressionEvaluator,
            TransactionManager = context.AsRuntimeExecutionContext().TransactionManager,
            ConnectorRegistry = _connectorRegistry ?? new MicroflowRuntimeConnectorRegistry(),
            RuntimeSecurityContext = MicroflowRuntimeSecurityContext.FromRequestContext(context.RequestContext, applyEntityAccess: true),
            Options = new MicroflowActionExecutionOptions
            {
                Mode = context.AsRuntimeExecutionContext().Mode,
                AllowRealHttp = context.Options.AllowRealHttp ?? false,
                SimulateRestError = context.Options.SimulateRestError ?? false,
                StopOnUnsupported = true,
                MaxCallDepth = context.MaxCallDepth
            },
            LoopExecutionOptions = new MicroflowLoopExecutionOptions
            {
                MaxIterations = context.Options.LoopIterations is > 0 ? context.Options.LoopIterations.Value : 1000,
                LoopIterationsOverride = context.Options.LoopIterations,
                StopOnActionError = true
            },
            LoopBodyExecutor = (iteration, ct) => ExecuteLoopBodyAsync(context, graph, node, iteration, ct)
        };

        var loopResult = await _loopExecutor.ExecuteLoopAsync(actionContext, actionContext.ExecutionNode, cancellationToken).ConfigureAwait(false);
        if (string.Equals(loopResult.Status, MicroflowLoopExecutionStatus.Failed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(loopResult.Status, MicroflowLoopExecutionStatus.Cancelled, StringComparison.OrdinalIgnoreCase)
            || string.Equals(loopResult.Status, MicroflowLoopExecutionStatus.MaxIterationsExceeded, StringComparison.OrdinalIgnoreCase))
        {
            var loopError = loopResult.Error ?? Error(RuntimeErrorCode.RuntimeLoopMaxIterationsExceeded, $"循环执行失败：{loopResult.Status}", node.Id, flowId: incomingFlowId);
            var handledLoopFailure = TryHandleConfiguredFailure(
                context,
                graph,
                node,
                action: null,
                incomingFlowId,
                loopError,
                actionResult: null,
                sourceExecutionNode: actionContext.ExecutionNode);
            if (handledLoopFailure is not null)
            {
                return handledLoopFailure;
            }

            context.AddNodeFailure(node, incomingFlowId, loopError);
            return NodeExecution.Failed(loopError);
        }

        return ContinueAfterAction(
            context,
            graph,
            node,
            incomingFlowId,
            loopResult.OutputPreview ?? JsonObj(new { status = loopResult.Status, iterations = loopResult.IterationCount }));
    }

    private async Task<MicroflowLoopBodyExecutionResult> ExecuteLoopBodyAsync(
        RuntimeContext context,
        MicroflowRuntimeGraph graph,
        MicroflowObjectModel loopNode,
        MicroflowLoopIterationContext iteration,
        CancellationToken cancellationToken)
    {
        var plan = context.ResolveExecutionPlan();
        var query = context.ResolveExecutionPlanQuery();
        var loopCollection = query.GetLoopCollection(plan, loopNode.Id);
        if (loopCollection is null)
        {
            return LoopBodyFailed(Error(RuntimeErrorCode.RuntimeLoopBodyNotFound, "Loop collection is missing.", loopNode.Id));
        }

        var entryNodeId = query.FindLoopEntryNodeId(plan, loopCollection);
        if (string.IsNullOrWhiteSpace(entryNodeId))
        {
            return LoopBodyFailed(Error(RuntimeErrorCode.RuntimeLoopBodyNotFound, "Loop body entry node is missing.", loopNode.Id));
        }

        var currentNodeId = entryNodeId;
        string? incomingFlowId = null;
        while (!string.IsNullOrWhiteSpace(currentNodeId))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!context.TryStep(out var stepError))
            {
                return new MicroflowLoopBodyExecutionResult
                {
                    Status = MicroflowLoopBodyExecutionStatus.MaxStepsExceeded,
                    Error = stepError
                };
            }

            if (!graph.Objects.TryGetValue(currentNodeId, out var node))
            {
                return LoopBodyFailed(Error(RuntimeErrorCode.RuntimeObjectNotFound, $"Loop body 运行对象不存在：{currentNodeId}", currentNodeId, flowId: incomingFlowId));
            }

            if (!string.Equals(node.CollectionId, loopCollection.CollectionId, StringComparison.Ordinal))
            {
                return LoopBodyFailed(Error(RuntimeErrorCode.RuntimeLoopDeadEnd, $"Loop body 节点不在当前 collection：{node.Id}", node.Id, flowId: incomingFlowId));
            }

            if (node.Kind == "breakEvent")
            {
                context.AddFrame(node, incomingFlowId, null, "success", JsonObj(new { node.Kind, signal = "break" }), iteration.LoopIterationJson, null, "Break current loop iteration.");
                return new MicroflowLoopBodyExecutionResult { Status = MicroflowLoopBodyExecutionStatus.Break };
            }

            if (node.Kind == "continueEvent")
            {
                context.AddFrame(node, incomingFlowId, null, "success", JsonObj(new { node.Kind, signal = "continue" }), iteration.LoopIterationJson, null, "Continue current loop iteration.");
                return new MicroflowLoopBodyExecutionResult { Status = MicroflowLoopBodyExecutionStatus.Continue };
            }

            var execution = await ExecuteNodeAsync(context, graph, node, incomingFlowId, new CallExecutionState(), cancellationToken).ConfigureAwait(false);
            if (!execution.Success)
            {
                return LoopBodyFailed(execution.Error ?? Error(RuntimeErrorCode.RuntimeLoopDeadEnd, "Loop body failed.", node.Id, flowId: incomingFlowId));
            }

            if (execution.Completed)
            {
                return new MicroflowLoopBodyExecutionResult
                {
                    Status = MicroflowLoopBodyExecutionStatus.Success,
                    Output = execution.Output
                };
            }

            currentNodeId = execution.NextNodeId;
            incomingFlowId = execution.OutgoingFlowId;
        }

        return LoopBodyFailed(Error(RuntimeErrorCode.RuntimeLoopDeadEnd, "Loop body did not reach a terminal node.", loopNode.Id));
    }

    private static MicroflowLoopBodyExecutionResult LoopBodyFailed(MicroflowRuntimeErrorDto error)
        => new()
        {
            Status = MicroflowLoopBodyExecutionStatus.Failed,
            Error = error
        };

    private static NodeExecution ExecuteErrorEvent(RuntimeContext context, MicroflowObjectModel node, string? incomingFlowId)
    {
        var message = ReadString(node.Raw, "message")
            ?? ReadString(node.Raw, "errorMessage")
            ?? "Microflow error event reached.";
        var errorCode = ReadString(node.Raw, "errorCode") ?? RuntimeErrorCode.RuntimeErrorEventReached;
        var error = Error(errorCode, message, node.Id, flowId: incomingFlowId);
        context.AddNodeFailure(node, incomingFlowId, error);
        return NodeExecution.Failed(error);
    }

    /// <summary>
    /// Annotation 节点不参与 runtime 业务语义，只做 pass-through：
    /// - 无 outgoing：作为画布注释终止运行的不可达路径，等同 Done(null)。
    /// - 有 normal outgoing：沿单条 normal outgoing 继续。
    /// </summary>
    private static NodeExecution ExecuteAnnotationPassThrough(RuntimeContext context, MicroflowRuntimeGraph graph, MicroflowObjectModel node, string? incomingFlowId)
    {
        var outgoing = graph.NormalOutgoing(node.Id);
        if (outgoing.Count == 0)
        {
            context.AddFrame(node, incomingFlowId, null, "success", JsonObj(new { node.Kind, message = "Annotation node skipped." }), JsonNull(), null, "Annotation node skipped.");
            return NodeExecution.Done(JsonNull());
        }

        var flow = outgoing[0];
        context.AddFrame(node, incomingFlowId, flow.Id, "success", JsonObj(new { node.Kind }), JsonObj(new { nextNodeId = flow.DestinationObjectId }), null, "Annotation node skipped.");
        return NodeExecution.Next(flow.DestinationObjectId!, flow.Id);
    }

    private static NodeExecution ExecuteGatewayPassThrough(RuntimeContext context, MicroflowRuntimeGraph graph, MicroflowObjectModel node, string? incomingFlowId, string gatewayKind)
    {
        var outgoing = graph.NormalOutgoing(node.Id);
        if (outgoing.Count == 0)
        {
            var error = Error(RuntimeErrorCode.RuntimeFlowNotFound, $"{gatewayKind} Gateway 至少需要一条 normal outgoing flow：{node.Id}", node.Id, flowId: incomingFlowId);
            context.AddNodeFailure(node, incomingFlowId, error);
            return NodeExecution.Failed(error);
        }

        var selected = SelectGatewayOutgoing(context, node, outgoing, gatewayKind, out var selectedCase, out var selectionError);
        if (selectionError is not null)
        {
            context.AddNodeFailure(node, incomingFlowId, selectionError);
            return NodeExecution.Failed(selectionError);
        }

        context.AddFrame(
            node,
            incomingFlowId,
            selected.Id,
            "success",
            JsonObj(new
            {
                node.Kind,
                gatewayKind,
                outgoingCount = outgoing.Count,
                selectedFlowId = selected.Id
            }),
            JsonObj(new
            {
                nextNodeId = selected.DestinationObjectId,
                selectedFlowId = selected.Id,
                selectedCase
            }),
            null,
            selectedCase);
        return NodeExecution.Next(selected.DestinationObjectId!, selected.Id);
    }

    private async Task<NodeExecution> ExecuteParallelGatewayAsync(
        RuntimeContext context,
        MicroflowRuntimeGraph graph,
        MicroflowObjectModel node,
        string? incomingFlowId,
        CallExecutionState state,
        CancellationToken cancellationToken)
    {
        var outgoing = graph.NormalOutgoing(node.Id);
        if (outgoing.Count <= 1)
        {
            return ExecuteGatewayPassThrough(context, graph, node, incomingFlowId, "parallel");
        }

        var joinNodeId = FindParallelJoinNodeId(graph, node.Id, outgoing);
        if (string.IsNullOrWhiteSpace(joinNodeId))
        {
            var error = Error(RuntimeErrorCode.RuntimeFlowNotFound, $"parallel Gateway 未找到可汇聚的 join 节点：{node.Id}", node.Id, flowId: incomingFlowId);
            context.AddNodeFailure(node, incomingFlowId, error);
            return NodeExecution.Failed(error);
        }

        var scheduler = new ParallelBranchScheduler();
        var joinStore = new InMemoryGatewayJoinStateStore();
        var splitInstanceId = SplitInstanceId.New(node.Id).ToString();
        var branchContexts = new List<(string BranchId, RuntimeContext Context)>(outgoing.Count);
        var requests = outgoing.Select((flow, index) =>
        {
            var branchId = string.IsNullOrWhiteSpace(flow.Id) ? $"branch-{index + 1}" : flow.Id;
            var branchContext = context.ForkForBranch(branchId, _variableScopeForker);
            branchContexts.Add((branchId, branchContext));
            return new MicroflowBranchExecutionRequest
            {
                BranchId = branchId,
                SplitInstanceId = splitInstanceId,
                ExecuteAsync = async ct =>
                {
                    joinStore.MarkArrived(splitInstanceId, branchId);
                    var result = await ExecuteParallelBranchAsync(branchContext, graph, flow.DestinationObjectId!, joinNodeId!, state, ct).ConfigureAwait(false);
                    if (result.Success)
                    {
                        joinStore.MarkCompleted(splitInstanceId, branchId);
                    }
                    else
                    {
                        joinStore.MarkFailed(splitInstanceId, branchId);
                    }

                    return result;
                }
            };
        }).ToArray();

        var results = await scheduler.RunAsync(requests, cancellationToken).ConfigureAwait(false);
        var conflictCodes = GatewayWriteConflictDetector.Detect(branchContexts.SelectMany(item => item.Context.CollectWriteIntents(item.BranchId)).ToArray());
        if (conflictCodes.Count > 0)
        {
            var conflict = conflictCodes[0];
            var errorCode = conflict.Split(':', 2)[0];
            var error = Error(errorCode, $"parallel 分支写入冲突：{conflict}", node.Id, flowId: incomingFlowId);
            context.AddNodeFailure(node, incomingFlowId, error);
            return NodeExecution.Failed(error);
        }

        var failed = results.FirstOrDefault(result => !result.Success);
        if (failed is not null)
        {
            var error = Error(
                failed.ErrorCode ?? RuntimeErrorCode.RuntimeUnknownError,
                failed.ErrorMessage ?? $"parallel 分支执行失败：{failed.BranchId}",
                node.Id,
                flowId: incomingFlowId);
            context.AddNodeFailure(node, incomingFlowId, error);
            return NodeExecution.Failed(error);
        }

        foreach (var branch in branchContexts)
        {
            context.MergeFromBranch(branch.Context);
        }

        _branchMergePolicy.Merge(
            context.VariableStore,
            branchContexts.Select(branch => new BranchExecutionContext
            {
                BranchId = branch.BranchId,
                VariableStore = branch.Context.VariableStore
            }).ToArray());

        context.AddFrame(
            node,
            incomingFlowId,
            outgoing[0].Id,
            "success",
            JsonObj(new
            {
                node.Kind,
                gatewayKind = "parallel",
                outgoingCount = outgoing.Count,
                splitInstanceId
            }),
            JsonObj(new
            {
                joinNodeId,
                completedBranches = results.Select(result => result.BranchId).OrderBy(id => id, StringComparer.Ordinal).ToArray()
            }),
            null);
        return NodeExecution.Next(joinNodeId!, outgoing[0].Id);
    }

    private async Task<NodeExecution> ExecuteInclusiveGatewayAsync(
        RuntimeContext context,
        MicroflowRuntimeGraph graph,
        MicroflowObjectModel node,
        string? incomingFlowId,
        CallExecutionState state,
        CancellationToken cancellationToken)
    {
        var outgoing = graph.NormalOutgoing(node.Id);
        if (outgoing.Count == 0)
        {
            var error = Error(RuntimeErrorCode.RuntimeFlowNotFound, $"inclusive Gateway 至少需要一条 normal outgoing flow：{node.Id}", node.Id, flowId: incomingFlowId);
            context.AddNodeFailure(node, incomingFlowId, error);
            return NodeExecution.Failed(error);
        }

        var selected = SelectInclusiveOutgoing(context, node, outgoing, out var selectedCases, out var selectionError);
        if (selectionError is not null)
        {
            context.AddNodeFailure(node, incomingFlowId, selectionError);
            return NodeExecution.Failed(selectionError);
        }

        if (selected.Count == 1)
        {
            var flow = selected[0];
            context.AddFrame(
                node,
                incomingFlowId,
                flow.Id,
                "success",
                JsonObj(new { node.Kind, gatewayKind = "inclusive", outgoingCount = outgoing.Count, selectedFlowIds = selected.Select(item => item.Id).ToArray() }),
                JsonObj(new { nextNodeId = flow.DestinationObjectId, selectedFlowIds = selected.Select(item => item.Id).ToArray(), selectedCases }),
                null);
            return NodeExecution.Next(flow.DestinationObjectId!, flow.Id);
        }

        var joinNodeId = FindGatewayJoinNodeId(graph, node.Id, selected, candidate => candidate.Kind is "inclusiveGateway" or "inclusiveMerge" or "parallelGateway" or "parallelMerge");
        if (string.IsNullOrWhiteSpace(joinNodeId))
        {
            var error = Error(RuntimeErrorCode.RuntimeFlowNotFound, $"inclusive Gateway 未找到可汇聚的 join 节点：{node.Id}", node.Id, flowId: incomingFlowId);
            context.AddNodeFailure(node, incomingFlowId, error);
            return NodeExecution.Failed(error);
        }

        var scheduler = new ParallelBranchScheduler();
        var joinStore = new InMemoryGatewayJoinStateStore();
        var splitInstanceId = SplitInstanceId.New(node.Id).ToString();
        var branchContexts = new List<(string BranchId, RuntimeContext Context)>(selected.Count);
        var requests = selected.Select((flow, index) =>
        {
            var branchId = string.IsNullOrWhiteSpace(flow.Id) ? $"inclusive-branch-{index + 1}" : flow.Id;
            var branchContext = context.ForkForBranch(branchId, _variableScopeForker);
            branchContexts.Add((branchId, branchContext));
            return new MicroflowBranchExecutionRequest
            {
                BranchId = branchId,
                SplitInstanceId = splitInstanceId,
                ExecuteAsync = async ct =>
                {
                    joinStore.MarkArrived(splitInstanceId, branchId);
                    var result = await ExecuteParallelBranchAsync(branchContext, graph, flow.DestinationObjectId!, joinNodeId!, state, ct).ConfigureAwait(false);
                    if (result.Success)
                    {
                        joinStore.MarkCompleted(splitInstanceId, branchId);
                    }
                    else
                    {
                        joinStore.MarkFailed(splitInstanceId, branchId);
                    }

                    return result;
                }
            };
        }).ToArray();

        var results = await scheduler.RunAsync(requests, cancellationToken).ConfigureAwait(false);
        var conflictCodes = GatewayWriteConflictDetector.Detect(branchContexts.SelectMany(item => item.Context.CollectWriteIntents(item.BranchId)).ToArray());
        if (conflictCodes.Count > 0)
        {
            var conflict = conflictCodes[0];
            var errorCode = conflict.Split(':', 2)[0];
            var error = Error(errorCode, $"inclusive 分支写入冲突：{conflict}", node.Id, flowId: incomingFlowId);
            context.AddNodeFailure(node, incomingFlowId, error);
            return NodeExecution.Failed(error);
        }

        var failed = results.FirstOrDefault(result => !result.Success);
        if (failed is not null)
        {
            var error = Error(
                failed.ErrorCode ?? RuntimeErrorCode.RuntimeUnknownError,
                failed.ErrorMessage ?? $"inclusive 分支执行失败：{failed.BranchId}",
                node.Id,
                flowId: incomingFlowId);
            context.AddNodeFailure(node, incomingFlowId, error);
            return NodeExecution.Failed(error);
        }

        foreach (var branch in branchContexts)
        {
            context.MergeFromBranch(branch.Context);
        }

        _branchMergePolicy.Merge(
            context.VariableStore,
            branchContexts.Select(branch => new BranchExecutionContext
            {
                BranchId = branch.BranchId,
                VariableStore = branch.Context.VariableStore
            }).ToArray());

        context.AddFrame(
            node,
            incomingFlowId,
            selected[0].Id,
            "success",
            JsonObj(new
            {
                node.Kind,
                gatewayKind = "inclusive",
                outgoingCount = outgoing.Count,
                selectedFlowIds = selected.Select(item => item.Id).ToArray(),
                splitInstanceId
            }),
            JsonObj(new
            {
                joinNodeId,
                selectedCases,
                completedBranches = results.Select(result => result.BranchId).OrderBy(id => id, StringComparer.Ordinal).ToArray()
            }),
            null);
        return NodeExecution.Next(joinNodeId!, selected[0].Id);
    }

    private static IReadOnlyList<MicroflowFlowModel> SelectInclusiveOutgoing(
        RuntimeContext context,
        MicroflowObjectModel node,
        IReadOnlyList<MicroflowFlowModel> outgoing,
        out JsonElement selectedCases,
        out MicroflowRuntimeErrorDto? error)
    {
        error = null;
        var selected = new List<MicroflowFlowModel>();
        var cases = new List<JsonElement>();
        MicroflowFlowModel? fallback = null;
        JsonElement? fallbackCase = null;

        foreach (var flow in outgoing)
        {
            if (flow.CaseValues.Count == 0)
            {
                fallback ??= flow;
                continue;
            }

            var matched = false;
            foreach (var caseValue in flow.CaseValues)
            {
                if (MicroflowRuntimeGraph.CaseMatches(caseValue, "otherwise")
                    || MicroflowRuntimeGraph.CaseMatches(caseValue, "else")
                    || MicroflowRuntimeGraph.CaseMatches(caseValue, "default"))
                {
                    fallback ??= flow;
                    fallbackCase ??= caseValue.Clone();
                    continue;
                }

                var expression = ReadExpressionText(caseValue)
                    ?? ReadString(caseValue, "condition")
                    ?? ReadString(caseValue, "expression");
                if (string.IsNullOrWhiteSpace(expression))
                {
                    continue;
                }

                var evaluated = context.EvaluateExpression(expression!, currentObjectId: node.Id);
                if (evaluated.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
                {
                    error = Error(RuntimeErrorCode.RuntimeExpressionError, $"Inclusive Gateway 条件必须返回 boolean：{node.Id}", node.Id);
                    selectedCases = JsonSerializer.SerializeToElement(Array.Empty<object>(), JsonOptions);
                    return Array.Empty<MicroflowFlowModel>();
                }

                if (evaluated.GetBoolean())
                {
                    matched = true;
                    cases.Add(caseValue.Clone());
                }
            }

            if (matched)
            {
                selected.Add(flow);
            }
        }

        if (selected.Count == 0 && fallback is not null)
        {
            selected.Add(fallback);
            if (fallbackCase.HasValue)
            {
                cases.Add(fallbackCase.Value);
            }
        }

        selectedCases = JsonSerializer.SerializeToElement(cases, JsonOptions);
        return selected;
    }

    private static MicroflowFlowModel SelectGatewayOutgoing(
        RuntimeContext context,
        MicroflowObjectModel node,
        IReadOnlyList<MicroflowFlowModel> outgoing,
        string gatewayKind,
        out JsonElement? selectedCase,
        out MicroflowRuntimeErrorDto? error)
    {
        selectedCase = null;
        error = null;
        if (outgoing.Count == 1)
        {
            return outgoing[0];
        }

        foreach (var flow in outgoing)
        {
            if (flow.CaseValues.Count == 0)
            {
                continue;
            }

            var caseValue = flow.CaseValues[0];
            var expression = ReadExpressionText(caseValue)
                ?? ReadString(caseValue, "condition")
                ?? ReadString(caseValue, "expression");
            if (string.IsNullOrWhiteSpace(expression))
            {
                if (MicroflowRuntimeGraph.CaseMatches(caseValue, "otherwise"))
                {
                    selectedCase = caseValue.Clone();
                    return flow;
                }

                continue;
            }

            var evaluated = context.EvaluateExpression(expression!, currentObjectId: node.Id);
            if (evaluated.ValueKind == JsonValueKind.True)
            {
                selectedCase = caseValue.Clone();
                return flow;
            }
        }

        var fallback = outgoing.FirstOrDefault(flow => flow.CaseValues.Any(caseValue =>
            MicroflowRuntimeGraph.CaseMatches(caseValue, "otherwise")
            || MicroflowRuntimeGraph.CaseMatches(caseValue, "else")
            || MicroflowRuntimeGraph.CaseMatches(caseValue, "default")))
            ?? outgoing[0];
        selectedCase = fallback.CaseValues.FirstOrDefault().ValueKind == JsonValueKind.Undefined
            ? null
            : fallback.CaseValues.First().Clone();
        return fallback;
    }

    private static NodeExecution ExecuteSingleOutgoing(RuntimeContext context, MicroflowRuntimeGraph graph, MicroflowObjectModel node, string? incomingFlowId, string label)
    {
        var outgoing = graph.NormalOutgoing(node.Id);
        if (outgoing.Count != 1)
        {
            var error = Error(RuntimeErrorCode.RuntimeFlowNotFound, $"{label} 节点必须有且仅有一条 normal outgoing flow：{node.Id}", node.Id, flowId: incomingFlowId);
            context.AddNodeFailure(node, incomingFlowId, error);
            return NodeExecution.Failed(error);
        }

        var flow = outgoing[0];
        context.AddFrame(node, incomingFlowId, flow.Id, "success", JsonObj(new { node.Kind }), JsonObj(new { nextNodeId = flow.DestinationObjectId }), null);
        return NodeExecution.Next(flow.DestinationObjectId!, flow.Id);
    }

    private static NodeExecution ExecuteEnd(RuntimeContext context, MicroflowObjectModel node, string? incomingFlowId)
    {
        var expression = ReadExpressionText(node.Raw, "returnValue") ?? ReadExpressionText(node.Raw, "returnValueExpression");
        JsonElement output;
        if (string.IsNullOrWhiteSpace(expression))
        {
            output = JsonNull();
        }
        else
        {
            output = context.EvaluateExpression(expression!, currentObjectId: node.Id);
        }

        context.AddFrame(node, incomingFlowId, null, "success", JsonObj(new { expression }), output, null, "End node reached.");
        return NodeExecution.Done(output);
    }

    private async Task<NodeExecution> ExecuteActionAsync(
        RuntimeContext context,
        MicroflowRuntimeGraph graph,
        MicroflowObjectModel node,
        string? incomingFlowId,
        CallExecutionState state,
        CancellationToken cancellationToken)
    {
        var action = node.Action;
        if (action is null)
        {
            var error = Error(RuntimeErrorCode.RuntimeUnsupportedAction, $"ActionActivity 缺少 action 配置：{node.Id}", node.Id, flowId: incomingFlowId);
            context.AddNodeFailure(node, incomingFlowId, error);
            return NodeExecution.Failed(error);
        }

        // Fast-path: the variable mutators have hand-tuned execution semantics
        // inside the engine (variable scope, fast variable typing). Keep them
        // in-engine to avoid double-bookkeeping. callMicroflow is no longer
        // short-circuited here (P0-5): when a CallMicroflowActionExecutor is
        // registered we route the call through the registry path so call stack,
        // execution plan, transaction boundary and return-value binding are
        // handled by a single implementation. Legacy in-engine path is used as
        // a fallback only when the executor is not registered.
        switch (action.Kind)
        {
            case "createVariable":
                return ExecuteCreateVariable(context, graph, node, action, incomingFlowId);
            case "changeVariable":
                return ExecuteChangeVariable(context, graph, node, action, incomingFlowId);
        }

        // Registry path (DI-only): retrieve / createObject / restCall / logMessage /
        // createList / changeList / aggregateList / break / continue / callMicroflow / etc.
        // Use the registry fallback for every non-inline action so known modeled
        // actions produce either real execution, runtimeCommands, or configured
        // connector-required failures instead of bypassing into an ad hoc branch.
        if (_actionExecutorRegistry is not null)
        {
            var executor = _actionExecutorRegistry.GetOrFallback(action.Kind);
            return await ExecuteActionViaRegistryAsync(
                context,
                graph,
                node,
                action,
                incomingFlowId,
                executor,
                cancellationToken);
        }

        // Fallback when no specialized executor is wired (e.g. legacy unit tests
        // that omit DI). Only callMicroflow has a non-trivial in-engine fallback
        // that preserves behavior of older tests.
        if (string.Equals(action.Kind, "callMicroflow", StringComparison.OrdinalIgnoreCase))
        {
            return await ExecuteCallMicroflowAsync(context, graph, node, action, incomingFlowId, state, cancellationToken);
        }

        return Unsupported(context, node, incomingFlowId, action.Kind);
    }

    private async Task<NodeExecution> ExecuteActionViaRegistryAsync(
        RuntimeContext context,
        MicroflowRuntimeGraph graph,
        MicroflowObjectModel node,
        MicroflowActionModel action,
        string? incomingFlowId,
        IMicroflowActionExecutor executor,
        CancellationToken cancellationToken)
    {
        var connectorRegistry = _connectorRegistry ?? new MicroflowRuntimeConnectorRegistry();
        var executionPlan = context.ResolveExecutionPlan();
        var executionNode = context.ResolveExecutionNode(node, action);
        var actionContext = new MicroflowActionExecutionContext
        {
            RuntimeExecutionContext = context.AsRuntimeExecutionContext(),
            ExecutionPlan = executionPlan,
            ExecutionNode = executionNode,
            ActionConfig = action.Raw,
            ActionKind = action.Kind,
            ObjectId = node.Id,
            ActionId = action.Id,
            CollectionId = node.CollectionId,
            VariableStore = context.VariableStore,
            ExpressionEvaluator = _expressionEvaluator,
            MetadataCatalog = context.Metadata,
            TransactionManager = context.AsRuntimeExecutionContext().TransactionManager,
            ConnectorRegistry = connectorRegistry,
            RuntimeSecurityContext = MicroflowRuntimeSecurityContext.FromRequestContext(context.RequestContext, applyEntityAccess: true),
            DebugCoordinator = _debugCoordinator,
            Options = new MicroflowActionExecutionOptions
            {
                Mode = context.AsRuntimeExecutionContext().Mode,
                AllowRealHttp = context.Options.AllowRealHttp ?? false,
                SimulateRestError = context.Options.SimulateRestError ?? false,
                StopOnUnsupported = true,
                MaxCallDepth = context.MaxCallDepth
            }
        };

        MicroflowActionExecutionResult result;
        try
        {
            result = await executor.ExecuteAsync(actionContext, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var error = Error(
                RuntimeErrorCode.RuntimeUnknownError,
                $"Action executor 抛出未处理异常：{ex.GetType().Name}",
                node.Id,
                action.Id,
                incomingFlowId,
                details: ex.Message);
            context.AddNodeFailure(node, incomingFlowId, error);
            return NodeExecution.Failed(error);
        }

        // Persist any logs the executor produced into the run trace.
        foreach (var log in result.Logs)
        {
            context.Logs.Add(log);
        }

        // Client-executed family: runtimeCommand 类动作在 executor 层仍返回
        // PendingClientCommand，但 RuntimeEngine 不再把它视为失败，而是把
        // 命令写入 trace/output 并沿 normal flow 继续，让前端可在同一 run
        // session 中消费这些命令。
        if (string.Equals(result.Status, MicroflowActionExecutionStatus.PendingClientCommand, StringComparison.OrdinalIgnoreCase))
        {
            var commandOutput = JsonObj(new
            {
                actionKind = action.Kind,
                status = result.Status,
                runtimeCommands = result.RuntimeCommands,
                message = result.Message,
                executorOutput = result.OutputJson
            });
            return ContinueAfterAction(context, graph, node, incomingFlowId, commandOutput);
        }

        // Failure / unsupported / connector-required paths halt the run with a
        // structured error frame; we never silently succeed.
        if (string.Equals(result.Status, MicroflowActionExecutionStatus.Failed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(result.Status, MicroflowActionExecutionStatus.Unsupported, StringComparison.OrdinalIgnoreCase)
            || string.Equals(result.Status, MicroflowActionExecutionStatus.ConnectorRequired, StringComparison.OrdinalIgnoreCase))
        {
            var error = result.Error ?? Error(
                RuntimeErrorCode.RuntimeUnsupportedAction,
                result.Message ?? $"Action {action.Kind} 执行失败。",
                node.Id,
                action.Id,
                incomingFlowId);
            error = error with
            {
                ObjectId = error.ObjectId ?? node.Id,
                ActionId = error.ActionId ?? action.Id,
                FlowId = error.FlowId ?? incomingFlowId
            };

            var handledFailure = TryHandleConfiguredFailure(
                context,
                graph,
                node,
                action,
                incomingFlowId,
                error,
                result,
                sourceExecutionNode: actionContext.ExecutionNode);
            if (handledFailure is not null)
            {
                return handledFailure;
            }

            context.AddNodeFailure(node, incomingFlowId, error);
            return NodeExecution.Failed(error);
        }

        // Success: bring produced variables back into the engine's variable map
        // so subsequent expressions can read them.
        foreach (var variable in result.ProducedVariables)
        {
            if (string.IsNullOrWhiteSpace(variable.Name))
            {
                continue;
            }

            var typeJson = variable.Type ?? Type("unknown");
            var rawValue = variable.RawValue ?? (variable.RawValueJson is null
                ? JsonNull()
                : (MicroflowVariableStore.ToJsonElement(variable.RawValueJson) ?? JsonNull()));
            context.SetVariable(variable.Name, typeJson, rawValue, $"action:{action.Kind}");
        }

        var output = result.OutputJson ?? JsonObj(new { actionKind = action.Kind, status = result.Status });
        return ContinueAfterAction(context, graph, node, incomingFlowId, output);
    }

    private static NodeExecution ExecuteCreateVariable(RuntimeContext context, MicroflowRuntimeGraph graph, MicroflowObjectModel node, MicroflowActionModel action, string? incomingFlowId)
    {
        var variableName = ReadString(action.Raw, "variableName");
        if (string.IsNullOrWhiteSpace(variableName))
        {
            var error = Error(RuntimeErrorCode.RuntimeVariableNotFound, $"Create Variable 缺少 variableName：{node.Id}", node.Id, action.Id, incomingFlowId);
            context.AddNodeFailure(node, incomingFlowId, error);
            return NodeExecution.Failed(error);
        }

        var type = action.Raw.TryGetProperty("dataType", out var dataType) ? dataType.Clone() : Type("unknown");
        var expression = ReadExpressionText(action.Raw, "initialValue") ?? ReadExpressionText(action.Raw, "initialValueExpression");
        var value = string.IsNullOrWhiteSpace(expression)
            ? JsonNull()
            : context.EvaluateExpression(expression!, currentObjectId: node.Id, currentActionId: action.Id);
        context.SetVariable(variableName!, type, value, "createVariable");

        return ContinueAfterAction(context, graph, node, incomingFlowId, JsonObj(new { variableName, value = ToPlainValue(value) }));
    }

    private static NodeExecution ExecuteChangeVariable(RuntimeContext context, MicroflowRuntimeGraph graph, MicroflowObjectModel node, MicroflowActionModel action, string? incomingFlowId)
    {
        var variableName = ReadString(action.Raw, "targetVariableName") ?? ReadString(action.Raw, "variableName");
        if (string.IsNullOrWhiteSpace(variableName))
        {
            var error = Error(RuntimeErrorCode.RuntimeVariableNotFound, $"Change Variable 缺少 targetVariableName：{node.Id}", node.Id, action.Id, incomingFlowId);
            context.AddNodeFailure(node, incomingFlowId, error);
            return NodeExecution.Failed(error);
        }

        if (!context.Variables.TryGetValue(variableName!, out var current))
        {
            var error = Error(RuntimeErrorCode.RuntimeVariableNotFound, $"变量不存在：{variableName}", node.Id, action.Id, incomingFlowId);
            context.AddNodeFailure(node, incomingFlowId, error);
            return NodeExecution.Failed(error);
        }

        var expression = ReadExpressionText(action.Raw, "newValueExpression") ?? ReadExpressionText(action.Raw, "valueExpression");
        if (string.IsNullOrWhiteSpace(expression))
        {
            var error = Error(RuntimeErrorCode.RuntimeExpressionError, $"Change Variable 缺少 newValueExpression：{variableName}", node.Id, action.Id, incomingFlowId);
            context.AddNodeFailure(node, incomingFlowId, error);
            return NodeExecution.Failed(error);
        }

        var value = context.EvaluateExpression(expression!, currentObjectId: node.Id, currentActionId: action.Id);
        context.SetVariable(variableName!, current.Type, value, "changeVariable");

        return ContinueAfterAction(context, graph, node, incomingFlowId, JsonObj(new { variableName, oldValue = ToPlainValue(current.Value), newValue = ToPlainValue(value) }));
    }

    private async Task<NodeExecution> ExecuteCallMicroflowAsync(
        RuntimeContext context,
        MicroflowRuntimeGraph graph,
        MicroflowObjectModel node,
        MicroflowActionModel action,
        string? incomingFlowId,
        CallExecutionState state,
        CancellationToken cancellationToken)
    {
        var callMode = ReadString(action.Raw, "callMode") ?? "sync";
        if (!string.Equals(callMode, "sync", StringComparison.OrdinalIgnoreCase))
        {
            var unsupportedCallModeError = Error(
                RuntimeErrorCode.RuntimeUnsupportedCallMode,
                "Async call microflow execution is not supported in Stage 23.",
                node.Id,
                action.Id,
                incomingFlowId,
                microflowId: context.ResourceId,
                callStack: context.CallStackPath);
            context.AddNodeFailure(node, incomingFlowId, unsupportedCallModeError);
            return NodeExecution.Failed(unsupportedCallModeError);
        }

        var targetMicroflowId = ReadString(action.Raw, "targetMicroflowId");
        if (string.IsNullOrWhiteSpace(targetMicroflowId))
        {
            var missingTargetError = Error(
                RuntimeErrorCode.RuntimeTargetMicroflowMissing,
                "CallMicroflow targetMicroflowId is required.",
                node.Id,
                action.Id,
                incomingFlowId,
                microflowId: context.ResourceId,
                callStack: context.CallStackPath);
            context.AddNodeFailure(node, incomingFlowId, missingTargetError);
            return NodeExecution.Failed(missingTargetError);
        }

        if (context.CallDepth >= context.MaxCallDepth)
        {
            var depthError = Error(
                RuntimeErrorCode.RuntimeCallStackOverflow,
                $"Max call depth exceeded ({context.MaxCallDepth}).",
                node.Id,
                action.Id,
                incomingFlowId,
                microflowId: context.ResourceId,
                callStack: context.CallStackPath);
            context.AddNodeFailure(node, incomingFlowId, depthError);
            return NodeExecution.Failed(depthError);
        }

        if (string.Equals(context.ResourceId, targetMicroflowId, StringComparison.Ordinal)
            || context.CallStackPath.Contains(targetMicroflowId, StringComparer.Ordinal))
        {
            var cyclePath = context.CallStackPath.Concat([targetMicroflowId]).ToArray();
            var recursionError = Error(
                RuntimeErrorCode.RuntimeCallRecursionDetected,
                $"Recursive microflow call detected: {string.Join(" -> ", cyclePath)}",
                node.Id,
                action.Id,
                incomingFlowId,
                microflowId: context.ResourceId,
                callStack: cyclePath);
            context.AddNodeFailure(node, incomingFlowId, recursionError);
            return NodeExecution.Failed(recursionError);
        }

        if (_resourceRepository is null || _schemaSnapshotRepository is null)
        {
            var repositoryMissingError = Error(
                RuntimeErrorCode.RuntimeTargetMicroflowNotFound,
                "Runtime microflow repository is unavailable.",
                node.Id,
                action.Id,
                incomingFlowId,
                microflowId: context.ResourceId,
                callStack: context.CallStackPath);
            context.AddNodeFailure(node, incomingFlowId, repositoryMissingError);
            return NodeExecution.Failed(repositoryMissingError);
        }

        var targetResource = await _resourceRepository.GetByIdAsync(targetMicroflowId, cancellationToken);
        if (targetResource is null)
        {
            var targetNotFoundError = Error(
                RuntimeErrorCode.RuntimeTargetMicroflowNotFound,
                $"Target microflow not found: {targetMicroflowId}",
                node.Id,
                action.Id,
                incomingFlowId,
                microflowId: context.ResourceId,
                callStack: context.CallStackPath);
            context.AddNodeFailure(node, incomingFlowId, targetNotFoundError);
            return NodeExecution.Failed(targetNotFoundError);
        }

        var snapshot = !string.IsNullOrWhiteSpace(targetResource.CurrentSchemaSnapshotId)
            ? await _schemaSnapshotRepository.GetByIdAsync(targetResource.CurrentSchemaSnapshotId!, cancellationToken)
            : null;
        snapshot ??= !string.IsNullOrWhiteSpace(targetResource.SchemaId)
            ? await _schemaSnapshotRepository.GetByIdAsync(targetResource.SchemaId!, cancellationToken)
            : null;
        snapshot ??= await _schemaSnapshotRepository.GetLatestByResourceIdAsync(targetResource.Id, cancellationToken);
        if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.SchemaJson))
        {
            var targetSchemaMissingError = Error(
                RuntimeErrorCode.RuntimeTargetMicroflowSchemaMissing,
                $"Target microflow schema not found: {targetResource.Id}",
                node.Id,
                action.Id,
                incomingFlowId,
                microflowId: context.ResourceId,
                callStack: context.CallStackPath);
            context.AddNodeFailure(node, incomingFlowId, targetSchemaMissingError);
            return NodeExecution.Failed(targetSchemaMissingError);
        }

        var targetSchema = MicroflowSchemaJsonHelper.ParseRequired(snapshot.SchemaJson);
        var targetModel = _schemaReader.Read(targetSchema);
        var boundInput = BindCallMicroflowParameters(action, targetModel, context, node, incomingFlowId, out var bindings, out var bindingError);
        if (bindingError is not null)
        {
            var outputWithBindingError = JsonObj(new
            {
                actionKind = "callMicroflow",
                targetMicroflowId,
                parameterBindings = bindings,
                callStack = context.CallStackPath
            });
            context.AddFrame(node, incomingFlowId, null, "failed", JsonObj(new { actionKind = "callMicroflow" }), outputWithBindingError, bindingError);
            return NodeExecution.Failed(bindingError);
        }

        var childRunId = Guid.NewGuid().ToString("N");
        var nextStack = context.CallStackPath.Concat([targetMicroflowId]).ToArray();
        var childSession = await RunInternalAsync(
            new MicroflowExecutionRequest
            {
                ResourceId = targetResource.Id,
                SchemaId = snapshot.Id,
                Version = targetResource.Version,
                Schema = targetSchema,
                Input = boundInput,
                Options = context.Options,
                RequestContext = context.RequestContext,
                CorrelationId = context.CorrelationId,
                MaxCallDepth = context.MaxCallDepth,
                DebugSessionId = context.DebugSessionId
            },
            state,
            new ParentCallContext(
                context.RunId,
                context.RootRunId,
                context.CorrelationId,
                context.CallDepth + 1,
                nextStack,
                node.Id,
                action.Id,
                childRunId),
            cancellationToken);

        context.AddChildRun(childSession);
        if (!string.Equals(childSession.Status, "success", StringComparison.OrdinalIgnoreCase))
        {
            var childFailedError = childSession.Error?.Code is RuntimeErrorCode.RuntimeCallRecursionDetected or RuntimeErrorCode.RuntimeCallStackOverflow
                ? Error(
                    childSession.Error.Code,
                    childSession.Error.Message,
                    node.Id,
                    action.Id,
                    incomingFlowId,
                    details: childSession.Error.Details,
                    microflowId: context.ResourceId,
                    callStack: childSession.Error.CallStack ?? nextStack)
                : Error(
                    RuntimeErrorCode.RuntimeChildMicroflowFailed,
                    $"Child microflow failed: {childSession.Error?.Message ?? childSession.Status}",
                    node.Id,
                    action.Id,
                    incomingFlowId,
                    details: childSession.Error is null ? null : JsonSerializer.Serialize(childSession.Error, JsonOptions),
                    microflowId: context.ResourceId,
                    callStack: nextStack);
            context.AddFrame(
                node,
                incomingFlowId,
                null,
                "failed",
                JsonObj(new { actionKind = "callMicroflow" }),
                JsonObj(new
                {
                    targetMicroflowId,
                    targetMicroflowQualifiedName = ReadString(action.Raw, "targetMicroflowQualifiedName"),
                    parameterBindings = bindings,
                    childRunId = childSession.Id,
                    childStatus = childSession.Status,
                    childError = childSession.Error
                }),
                childFailedError);
            return NodeExecution.Failed(childFailedError);
        }

        var returnBindingError = TryBindCallMicroflowReturnValue(context, action, targetModel, childSession, node, incomingFlowId);
        if (returnBindingError is not null)
        {
            context.AddFrame(
                node,
                incomingFlowId,
                null,
                "failed",
                JsonObj(new { actionKind = "callMicroflow" }),
                JsonObj(new
                {
                    targetMicroflowId,
                    parameterBindings = bindings,
                    childRunId = childSession.Id,
                    childStatus = childSession.Status
                }),
                returnBindingError);
            return NodeExecution.Failed(returnBindingError);
        }

        var callOutput = JsonObj(new
        {
            actionKind = "callMicroflow",
            targetMicroflowId,
            targetMicroflowQualifiedName = ReadString(action.Raw, "targetMicroflowQualifiedName"),
            parameterBindings = bindings,
            childRunId = childSession.Id,
            childStatus = childSession.Status,
            childOutput = childSession.Output,
            childTrace = childSession.Trace.Select(frame => new
            {
                frame.ObjectId,
                frame.ActionId,
                frame.Status,
                frame.MicroflowId,
                frame.Error
            }).ToArray(),
            callStack = nextStack
        });

        return ContinueAfterAction(context, graph, node, incomingFlowId, callOutput);
    }

    private static NodeExecution ContinueAfterAction(RuntimeContext context, MicroflowRuntimeGraph graph, MicroflowObjectModel node, string? incomingFlowId, JsonElement output)
    {
        var outgoing = graph.NormalOutgoing(node.Id);
        if (outgoing.Count == 0 && node.InsideLoop)
        {
            context.AddFrame(node, incomingFlowId, null, "success", JsonObj(new { actionKind = node.Action?.Kind, loopBodyTerminal = true }), output, null);
            return NodeExecution.Done(output);
        }

        if (outgoing.Count != 1)
        {
            var error = Error(RuntimeErrorCode.RuntimeFlowNotFound, $"Action 节点必须有且仅有一条 normal outgoing flow：{node.Id}", node.Id, node.Action?.Id, incomingFlowId);
            context.AddNodeFailure(node, incomingFlowId, error);
            return NodeExecution.Failed(error);
        }

        var flow = outgoing[0];
        context.AddFrame(node, incomingFlowId, flow.Id, "success", JsonObj(new { actionKind = node.Action?.Kind }), output, null);
        return NodeExecution.Next(flow.DestinationObjectId!, flow.Id);
    }

    private NodeExecution? TryHandleConfiguredFailure(
        RuntimeContext context,
        MicroflowRuntimeGraph graph,
        MicroflowObjectModel node,
        MicroflowActionModel? action,
        string? incomingFlowId,
        MicroflowRuntimeErrorDto error,
        MicroflowActionExecutionResult? actionResult,
        MicroflowExecutionNode sourceExecutionNode)
    {
        if (_errorHandlingService is null)
        {
            return null;
        }

            var errorHandlingType = action is not null
            ? ReadString(action.Raw, "errorHandlingType") ?? ReadStringByPath(action.Raw, "errorHandling", "type")
            : ReadString(node.Raw, "errorHandlingType") ?? ReadStringByPath(node.Raw, "errorHandling", "type");
        if (string.IsNullOrWhiteSpace(errorHandlingType)
            && actionResult?.ShouldEnterErrorHandler == true
            && graph.ErrorHandlerOutgoing(node.Id).Count > 0)
        {
            errorHandlingType = MicroflowErrorHandlingType.CustomWithoutRollback;
        }

        if (string.IsNullOrWhiteSpace(errorHandlingType))
        {
            return null;
        }

        var runtimeContext = context.AsRuntimeExecutionContext();
        var normalOutgoingFlow = graph.NormalOutgoing(node.Id).FirstOrDefault();
        var handling = _errorHandlingService.Handle(new MicroflowErrorHandlingContext
        {
            RuntimeContext = runtimeContext,
            Plan = context.ResolveExecutionPlan(),
            SourceNode = sourceExecutionNode,
            ActionResult = actionResult ?? new MicroflowActionExecutionResult { Error = error, Status = MicroflowActionExecutionStatus.Failed },
            Error = error,
            ErrorHandlingType = errorHandlingType!,
            SourceObjectId = node.Id,
            SourceActionId = action?.Id,
            CollectionId = node.CollectionId,
            IncomingFlowId = incomingFlowId,
            NormalOutgoingFlowId = normalOutgoingFlow?.Id,
            LatestHttpResponse = actionResult?.LatestHttpResponse,
            ErrorDepth = runtimeContext.ErrorStack.Count
        });

        if (handling.ShouldContinueNormalFlow && normalOutgoingFlow is not null)
        {
            context.AddNodeFailure(node, incomingFlowId, handling.Error ?? error, normalOutgoingFlow.Id);
            return NodeExecution.Next(normalOutgoingFlow.DestinationObjectId!, normalOutgoingFlow.Id);
        }

        if (!string.IsNullOrWhiteSpace(handling.NextFlowId)
            && graph.Flows.TryGetValue(handling.NextFlowId!, out var errorFlow)
            && !string.IsNullOrWhiteSpace(errorFlow.DestinationObjectId))
        {
            context.AddNodeFailure(node, incomingFlowId, handling.Error ?? error, errorFlow.Id);
            context.SetVariable(
                "$latestError",
                Type("Object"),
                JsonSerializer.SerializeToElement(handling.Error ?? error, JsonOptions),
                $"errorHandling:{errorHandlingType}");
            if (actionResult?.LatestHttpResponse.HasValue == true)
            {
                context.SetVariable(
                    "$latestHttpResponse",
                    Type("Object"),
                    actionResult.LatestHttpResponse.Value,
                    $"errorHandling:{errorHandlingType}");
            }

            return NodeExecution.Next(errorFlow.DestinationObjectId!, errorFlow.Id);
        }

        if (handling.ShouldStopRun)
        {
            return NodeExecution.Failed(handling.Error ?? error);
        }

        return null;
    }

    private static NodeExecution ExecuteDecision(RuntimeContext context, MicroflowRuntimeGraph graph, MicroflowObjectModel node, string? incomingFlowId)
    {
        var expression = ReadExpressionTextByPath(node.Raw, "splitCondition", "expression")
            ?? ReadExpressionTextByPath(node.Raw, "config", "expression")
            ?? ReadExpressionText(node.Raw, "expression");
        if (string.IsNullOrWhiteSpace(expression))
        {
            var error = Error(RuntimeErrorCode.RuntimeExpressionError, $"Decision expression 不能为空：{node.Id}", node.Id, flowId: incomingFlowId);
            context.AddNodeFailure(node, incomingFlowId, error);
            return NodeExecution.Failed(error);
        }

        var result = context.EvaluateExpression(expression!, currentObjectId: node.Id);
        if (result.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
        {
            var error = Error(RuntimeErrorCode.RuntimeExpressionError, $"Decision expression 必须返回 boolean：{node.Id}", node.Id, flowId: incomingFlowId);
            context.AddNodeFailure(node, incomingFlowId, error);
            return NodeExecution.Failed(error);
        }

        var selectedValue = result.GetBoolean();
        var selected = graph.SelectBooleanCaseFlow(node.Id, selectedValue);
        if (!selected.Success)
        {
            context.AddNodeFailure(node, incomingFlowId, selected.Error!);
            return NodeExecution.Failed(selected.Error!);
        }

        context.AddFrame(
            node,
            incomingFlowId,
            selected.Flow!.Id,
            "success",
            JsonObj(new { expression }),
            JsonObj(new { expressionResult = selectedValue, selectedFlowId = selected.Flow.Id }),
            null,
            selected.CaseValue);
        return NodeExecution.Next(selected.Flow.DestinationObjectId!, selected.Flow.Id);
    }

    private static NodeExecution ExecuteObjectTypeDecision(RuntimeContext context, MicroflowRuntimeGraph graph, MicroflowObjectModel node, string? incomingFlowId)
    {
        var inputVariableName = ReadString(node.Raw, "inputObjectVariableName")
            ?? ReadString(node.Raw, "inputObject")
            ?? ReadStringByPath(node.Raw, "config", "inputObjectVariableName")
            ?? ReadStringByPath(node.Raw, "config", "inputObject");
        if (string.IsNullOrWhiteSpace(inputVariableName))
        {
            var error = Error(RuntimeErrorCode.RuntimeVariableNotFound, $"Object Type Decision 缺少 inputObjectVariableName：{node.Id}", node.Id, flowId: incomingFlowId);
            context.AddNodeFailure(node, incomingFlowId, error);
            return NodeExecution.Failed(error);
        }

        var variableName = NormalizeVariableName(inputVariableName);
        var outgoing = graph.NormalOutgoing(node.Id);
        if (outgoing.Count == 0)
        {
            var error = Error(RuntimeErrorCode.RuntimeFlowNotFound, $"Object Type Decision 至少需要一条 outgoing flow：{node.Id}", node.Id, flowId: incomingFlowId);
            context.AddNodeFailure(node, incomingFlowId, error);
            return NodeExecution.Failed(error);
        }

        var hasValue = context.VariableStore.TryGet(variableName, out var variable)
            && variable is not null
            && !string.IsNullOrWhiteSpace(variable.RawValueJson);
        JsonElement? objectValue = hasValue ? MicroflowVariableStore.ToJsonElement(variable!.RawValueJson!) : null;
        if (!hasValue || objectValue is null || objectValue.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            var emptyFlow = FindObjectTypeFlow(outgoing, "empty") ?? FindObjectTypeFlow(outgoing, "noCase") ?? FindObjectTypeFlow(outgoing, "fallback");
            if (emptyFlow is null)
            {
                var error = Error(RuntimeErrorCode.RuntimeInvalidCase, $"Object Type Decision 未找到 empty/fallback 分支：{node.Id}", node.Id, flowId: incomingFlowId);
                context.AddNodeFailure(node, incomingFlowId, error);
                return NodeExecution.Failed(error);
            }

            return ContinueObjectType(context, node, incomingFlowId, emptyFlow.Value.Flow, emptyFlow.Value.CaseValue, actualEntity: null);
        }

        var actualEntity = ReadString(objectValue.Value, "entityType")
            ?? ReadString(objectValue.Value, "entityQualifiedName")
            ?? ReadString(objectValue.Value, "$entity")
            ?? TryReadEntityFromDataType(variable!.DataTypeJson);
        if (string.IsNullOrWhiteSpace(actualEntity))
        {
            var error = Error(RuntimeErrorCode.RuntimeVariableTypeMismatch, $"Object Type Decision 无法识别对象变量实体：{variableName}", node.Id, flowId: incomingFlowId);
            context.AddNodeFailure(node, incomingFlowId, error);
            return NodeExecution.Failed(error);
        }

        foreach (var flow in outgoing)
        {
            foreach (var caseValue in flow.CaseValues)
            {
                var token = CaseToken(caseValue);
                if (string.IsNullOrWhiteSpace(token)
                    || IsFallbackObjectTypeCase(token)
                    || string.Equals(token, "empty", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(token, "noCase", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (IsEntityAssignable(actualEntity, token!, context.Metadata))
                {
                    return ContinueObjectType(context, node, incomingFlowId, flow, caseValue, actualEntity);
                }
            }
        }

        var fallback = FindObjectTypeFlow(outgoing, "fallback") ?? FindObjectTypeFlow(outgoing, "noCase");
        if (fallback is null)
        {
            var error = Error(RuntimeErrorCode.RuntimeInvalidCase, $"Object Type Decision 未找到匹配分支：{actualEntity}", node.Id, flowId: incomingFlowId);
            context.AddNodeFailure(node, incomingFlowId, error);
            return NodeExecution.Failed(error);
        }

        return ContinueObjectType(context, node, incomingFlowId, fallback.Value.Flow, fallback.Value.CaseValue, actualEntity);
    }

    private static NodeExecution ContinueObjectType(RuntimeContext context, MicroflowObjectModel node, string? incomingFlowId, MicroflowFlowModel selectedFlow, JsonElement selectedCase, string? actualEntity)
    {
        context.AddFrame(
            node,
            incomingFlowId,
            selectedFlow.Id,
            "success",
            JsonObj(new { actualEntity }),
            JsonObj(new { actualEntity, selectedFlowId = selectedFlow.Id, selectedCase }),
            null,
            selectedCase);
        return NodeExecution.Next(selectedFlow.DestinationObjectId!, selectedFlow.Id);
    }

    private static (MicroflowFlowModel Flow, JsonElement CaseValue)? FindObjectTypeFlow(IReadOnlyList<MicroflowFlowModel> outgoing, string expected)
    {
        foreach (var flow in outgoing)
        {
            foreach (var caseValue in flow.CaseValues)
            {
                if (MicroflowRuntimeGraph.CaseMatches(caseValue, expected))
                {
                    return (flow, caseValue.Clone());
                }
            }
        }

        return null;
    }

    private static bool IsFallbackObjectTypeCase(string value)
        => value.Equals("fallback", StringComparison.OrdinalIgnoreCase)
           || value.Equals("otherwise", StringComparison.OrdinalIgnoreCase)
           || value.Equals("else", StringComparison.OrdinalIgnoreCase)
           || value.Equals("default", StringComparison.OrdinalIgnoreCase);

    private static string? CaseToken(JsonElement caseValue)
    {
        if (caseValue.ValueKind == JsonValueKind.Object)
        {
            return ReadString(caseValue, "entityQualifiedName")
                ?? ReadString(caseValue, "persistedValue")
                ?? ReadString(caseValue, "value")
                ?? ReadString(caseValue, "conditionKey")
                ?? ReadString(caseValue, "kind");
        }

        return caseValue.ValueKind switch
        {
            JsonValueKind.String => caseValue.GetString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => caseValue.GetRawText(),
            _ => null
        };
    }

    private static bool IsEntityAssignable(string actualEntity, string targetEntity, MicroflowMetadataCatalogDto? catalog)
    {
        if (string.Equals(actualEntity, targetEntity, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var current = actualEntity;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (catalog?.Entities.FirstOrDefault(entity => string.Equals(entity.QualifiedName, current, StringComparison.OrdinalIgnoreCase)) is { } entity
               && !string.IsNullOrWhiteSpace(entity.Generalization)
               && visited.Add(current))
        {
            current = entity.Generalization!;
            if (string.Equals(current, targetEntity, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? TryReadEntityFromDataType(string? dataTypeJson)
    {
        if (string.IsNullOrWhiteSpace(dataTypeJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(dataTypeJson);
            return ReadString(document.RootElement, "entityQualifiedName")
                ?? ReadString(document.RootElement, "entityType")
                ?? ReadString(document.RootElement, "qualifiedName");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string NormalizeVariableName(string variableName)
    {
        var value = variableName.Trim();
        return value.StartsWith("$", StringComparison.Ordinal) ? value[1..] : value;
    }

    private async Task<MicroflowBranchExecutionResult> ExecuteParallelBranchAsync(
        RuntimeContext context,
        MicroflowRuntimeGraph graph,
        string startNodeId,
        string joinNodeId,
        CallExecutionState state,
        CancellationToken cancellationToken)
    {
        var currentNodeId = startNodeId;
        string? incomingFlowId = null;
        while (!string.IsNullOrWhiteSpace(currentNodeId) && !string.Equals(currentNodeId, joinNodeId, StringComparison.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!context.TryStep(out var stepError))
            {
                return new MicroflowBranchExecutionResult
                {
                    BranchId = context.BranchId ?? currentNodeId,
                    Success = false,
                    ErrorCode = stepError.Code,
                    ErrorMessage = stepError.Message
                };
            }

            if (!graph.Objects.TryGetValue(currentNodeId, out var node))
            {
                return new MicroflowBranchExecutionResult
                {
                    BranchId = context.BranchId ?? currentNodeId,
                    Success = false,
                    ErrorCode = RuntimeErrorCode.RuntimeObjectNotFound,
                    ErrorMessage = $"parallel 分支节点不存在：{currentNodeId}"
                };
            }

            var execution = await ExecuteNodeAsync(context, graph, node, incomingFlowId, state, cancellationToken).ConfigureAwait(false);
            if (!execution.Success)
            {
                return new MicroflowBranchExecutionResult
                {
                    BranchId = context.BranchId ?? currentNodeId,
                    Success = false,
                    ErrorCode = execution.Error?.Code,
                    ErrorMessage = execution.Error?.Message
                };
            }

            if (execution.Completed)
            {
                return new MicroflowBranchExecutionResult
                {
                    BranchId = context.BranchId ?? currentNodeId,
                    Success = false,
                    ErrorCode = RuntimeErrorCode.RuntimeEndNotReached,
                    ErrorMessage = $"parallel 分支在 join 前提前结束：{currentNodeId}"
                };
            }

            currentNodeId = execution.NextNodeId;
            incomingFlowId = execution.OutgoingFlowId;
        }

        return new MicroflowBranchExecutionResult
        {
            BranchId = context.BranchId ?? startNodeId,
            Success = string.Equals(currentNodeId, joinNodeId, StringComparison.Ordinal)
        };
    }

    private static string? FindParallelJoinNodeId(
        MicroflowRuntimeGraph graph,
        string splitNodeId,
        IReadOnlyList<MicroflowFlowModel> outgoing)
        => FindGatewayJoinNodeId(graph, splitNodeId, outgoing, candidate => candidate.Kind is "parallelGateway" or "parallelMerge");

    private static string? FindGatewayJoinNodeId(
        MicroflowRuntimeGraph graph,
        string splitNodeId,
        IReadOnlyList<MicroflowFlowModel> outgoing,
        Func<MicroflowObjectModel, bool> isJoinCandidate)
    {
        var distancesPerBranch = outgoing
            .Where(flow => !string.IsNullOrWhiteSpace(flow.DestinationObjectId))
            .Select(flow => graph.ComputeDistances(flow.DestinationObjectId!, stopAtNodeId: splitNodeId))
            .ToArray();
        if (distancesPerBranch.Length != outgoing.Count)
        {
            return null;
        }

        var candidateIds = distancesPerBranch
            .Select(map => map.Keys.ToHashSet(StringComparer.Ordinal))
            .Aggregate((left, right) =>
            {
                left.IntersectWith(right);
                return left;
            })
            .Where(candidateId => !string.Equals(candidateId, splitNodeId, StringComparison.Ordinal))
            .Where(candidateId => graph.Objects.TryGetValue(candidateId, out var candidate)
                && isJoinCandidate(candidate))
            .ToArray();
        return candidateIds
            .OrderBy(candidateId => distancesPerBranch.Sum(map => map.GetValueOrDefault(candidateId, int.MaxValue)))
            .FirstOrDefault();
    }

    private static NodeExecution Unsupported(RuntimeContext context, MicroflowObjectModel node, string? incomingFlowId, string? actionKind = null)
    {
        var nodeType = actionKind ?? node.Kind;
        var error = Error(
            RuntimeErrorCode.RuntimeUnsupportedAction,
            $"Unsupported node type hit during Stage 22 runtime: nodeId={node.Id}, nodeName={node.Caption ?? node.Id}, nodeType={nodeType}",
            node.Id,
            node.Action?.Id,
            incomingFlowId);
        context.AddNodeFailure(node, incomingFlowId, error);
        return NodeExecution.Failed(error);
    }

    private static IReadOnlyDictionary<string, JsonElement> BindCallMicroflowParameters(
        MicroflowActionModel callAction,
        MicroflowSchemaModel targetModel,
        RuntimeContext context,
        MicroflowObjectModel node,
        string? incomingFlowId,
        out IReadOnlyList<object> bindings,
        out MicroflowRuntimeErrorDto? error)
    {
        var mappingRows = ReadCallParameterMappings(callAction.Raw);
        var mappingByName = mappingRows
            .Where(row => !string.IsNullOrWhiteSpace(row.TargetParameterName))
            .ToDictionary(row => row.TargetParameterName!, row => row, StringComparer.Ordinal);
        var mappingById = mappingRows
            .Where(row => !string.IsNullOrWhiteSpace(row.TargetParameterId))
            .ToDictionary(row => row.TargetParameterId!, row => row, StringComparer.Ordinal);
        var input = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var bindingRows = new List<object>();
        error = null;

        foreach (var parameter in targetModel.Parameters)
        {
            var required = ReadBool(parameter.Raw, "required");
            mappingByName.TryGetValue(parameter.Name, out var mappingByParameterName);
            mappingById.TryGetValue(parameter.Id, out var mappingByParameterId);
            var mapping = mappingByParameterName ?? mappingByParameterId;
            if (mapping is null)
            {
                if (required)
                {
                    error = Error(
                        RuntimeErrorCode.RuntimeParameterMappingMissing,
                        $"Required parameter mapping is missing: {parameter.Name}",
                        node.Id,
                        callAction.Id,
                        incomingFlowId,
                        microflowId: context.ResourceId,
                        callStack: context.CallStackPath);
                    bindingRows.Add(new { parameter.Name, status = "failed", code = RuntimeErrorCode.RuntimeParameterMappingMissing, message = error.Message });
                    bindings = bindingRows;
                    return input;
                }

                if (TryReadDefaultValue(parameter.Raw, out var defaultValue))
                {
                    var convertedDefault = ConvertToType(defaultValue, parameter.Type, parameter.Name, out var convertError);
                    if (convertedDefault is null || convertError is not null)
                    {
                        error = convertError ?? Error(RuntimeErrorCode.RuntimeParameterMappingFailed, $"Default value type coercion failed: {parameter.Name}", node.Id, callAction.Id, incomingFlowId, microflowId: context.ResourceId, callStack: context.CallStackPath);
                        bindingRows.Add(new { parameter.Name, status = "failed", code = RuntimeErrorCode.RuntimeParameterMappingFailed, message = error.Message });
                        bindings = bindingRows;
                        return input;
                    }

                    input[parameter.Name] = convertedDefault.Value;
                    bindingRows.Add(new { parameter.Name, status = "defaulted", value = ToPlainValue(convertedDefault.Value) });
                }
                else
                {
                    input[parameter.Name] = JsonNull();
                    bindingRows.Add(new { parameter.Name, status = "defaultedNull" });
                }

                continue;
            }

            JsonElement value;
            if (!string.IsNullOrWhiteSpace(mapping.SourceVariableName))
            {
                if (!context.Variables.TryGetValue(mapping.SourceVariableName!, out var sourceVariable))
                {
                    error = Error(RuntimeErrorCode.RuntimeParameterMappingMissing, $"Mapped source variable not found: {mapping.SourceVariableName}", node.Id, callAction.Id, incomingFlowId, microflowId: context.ResourceId, callStack: context.CallStackPath);
                    bindingRows.Add(new { parameter.Name, mapping.SourceVariableName, status = "failed", code = RuntimeErrorCode.RuntimeParameterMappingMissing, message = error.Message });
                    bindings = bindingRows;
                    return input;
                }

                value = sourceVariable.Value;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(mapping.Expression))
                {
                    error = Error(RuntimeErrorCode.RuntimeParameterMappingMissing, $"Parameter mapping expression is empty: {parameter.Name}", node.Id, callAction.Id, incomingFlowId, microflowId: context.ResourceId, callStack: context.CallStackPath);
                    bindingRows.Add(new { parameter.Name, status = "failed", code = RuntimeErrorCode.RuntimeParameterMappingMissing, message = error.Message });
                    bindings = bindingRows;
                    return input;
                }

                try
                {
                    value = context.EvaluateExpression(mapping.Expression!, currentObjectId: node.Id, currentActionId: callAction.Id);
                }
                catch (RuntimeExpressionException expressionException)
                {
                    error = Error(
                        RuntimeErrorCode.RuntimeParameterMappingFailed,
                        $"Parameter mapping expression failed: {parameter.Name}",
                        node.Id,
                        callAction.Id,
                        incomingFlowId,
                        details: expressionException.Error.Message,
                        microflowId: context.ResourceId,
                        callStack: context.CallStackPath);
                    bindingRows.Add(new { parameter.Name, mapping.Expression, status = "failed", code = RuntimeErrorCode.RuntimeParameterMappingFailed, message = error.Message });
                    bindings = bindingRows;
                    return input;
                }
            }

            var converted = ConvertToType(value, parameter.Type, parameter.Name, out var coercionError);
            if (converted is null || coercionError is not null)
            {
                error = coercionError ?? Error(RuntimeErrorCode.RuntimeParameterMappingFailed, $"Parameter mapping type coercion failed: {parameter.Name}", node.Id, callAction.Id, incomingFlowId, microflowId: context.ResourceId, callStack: context.CallStackPath);
                bindingRows.Add(new { parameter.Name, status = "failed", code = RuntimeErrorCode.RuntimeParameterMappingFailed, message = error.Message });
                bindings = bindingRows;
                return input;
            }

            input[parameter.Name] = converted.Value;
            bindingRows.Add(new
            {
                parameter.Name,
                mapping.SourceVariableName,
                mapping.Expression,
                status = "success",
                value = ToPlainValue(converted.Value)
            });
        }

        var knownParameterNames = targetModel.Parameters.Select(parameter => parameter.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var mapping in mappingRows.Where(row => !string.IsNullOrWhiteSpace(row.TargetParameterName) && !knownParameterNames.Contains(row.TargetParameterName!)))
        {
            bindingRows.Add(new
            {
                parameterName = mapping.TargetParameterName,
                mapping.Expression,
                mapping.SourceVariableName,
                status = "ignored",
                warning = "Unknown target parameter"
            });
        }

        bindings = bindingRows;
        return input;
    }

    private static MicroflowRuntimeErrorDto? TryBindCallMicroflowReturnValue(
        RuntimeContext context,
        MicroflowActionModel callAction,
        MicroflowSchemaModel targetModel,
        MicroflowRunSessionDto childSession,
        MicroflowObjectModel node,
        string? incomingFlowId)
    {
        var storeResult = ReadBoolByPath(callAction.Raw, "returnValue", "storeResult");
        var outputVariableName = ReadStringByPath(callAction.Raw, "returnValue", "outputVariableName")
            ?? ReadString(callAction.Raw, "outputVariableName")
            ?? ReadString(callAction.Raw, "resultVariableName");
        if (!storeResult || string.IsNullOrWhiteSpace(outputVariableName))
        {
            return null;
        }

        var targetReturnKind = targetModel.ReturnType.HasValue ? ReadTypeKind(targetModel.ReturnType.Value) : "void";
        if (string.Equals(targetReturnKind, "void", StringComparison.OrdinalIgnoreCase))
        {
            return Error(
                RuntimeErrorCode.RuntimeReturnBindingFailed,
                "Return binding is configured but target microflow return type is void.",
                node.Id,
                callAction.Id,
                incomingFlowId,
                microflowId: context.ResourceId,
                callStack: context.CallStackPath);
        }

        if (!childSession.Output.HasValue || childSession.Output.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return Error(
                RuntimeErrorCode.RuntimeReturnBindingFailed,
                "Child microflow did not produce a return value.",
                node.Id,
                callAction.Id,
                incomingFlowId,
                microflowId: context.ResourceId,
                callStack: context.CallStackPath);
        }

        var value = childSession.Output.Value;
        if (context.Variables.TryGetValue(outputVariableName!, out var existing))
        {
            context.SetVariable(outputVariableName!, existing.Type, value, "callMicroflow");
            return null;
        }

        var outputType = targetModel.ReturnType ?? Type("unknown");
        context.SetVariable(outputVariableName!, outputType, value, "callMicroflow");
        return null;
    }

    private static IReadOnlyList<CallParameterMapping> ReadCallParameterMappings(JsonElement actionRaw)
    {
        if (!actionRaw.TryGetProperty("parameterMappings", out var mappingsElement) || mappingsElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<CallParameterMapping>();
        }

        return mappingsElement.EnumerateArray()
            .Select(mapping => new CallParameterMapping(
                ReadString(mapping, "parameterName") ?? ReadString(mapping, "targetParameterName"),
                ReadString(mapping, "parameterId") ?? ReadString(mapping, "targetParameterId"),
                ReadExpressionText(mapping, "argumentExpression")
                    ?? ReadExpressionText(mapping, "expression")
                    ?? ReadExpressionText(mapping, "valueExpression"),
                ReadString(mapping, "sourceVariableName"),
                ReadString(mapping, "sourceVariableId")))
            .ToArray();
    }

    private static JsonElement? ConvertToType(JsonElement value, JsonElement type, string name, out MicroflowRuntimeErrorDto? error)
    {
        error = null;
        var kind = ReadTypeKind(type);
        try
        {
            return kind switch
            {
                "string" => value.ValueKind == JsonValueKind.String ? value.Clone() : JsonSerializer.SerializeToElement(value.ValueKind == JsonValueKind.Null ? null : value.GetRawText(), JsonOptions),
                "integer" or "long" => ConvertInteger(value, name, out error),
                "decimal" or "number" => ConvertDecimal(value, name, out error),
                "boolean" => ConvertBoolean(value, name, out error),
                "dateTime" => ConvertDateTime(value, name, out error),
                "object" or "json" => value.ValueKind is JsonValueKind.Object or JsonValueKind.Null ? value.Clone() : FailConvert(name, "object", out error),
                "list" => value.ValueKind is JsonValueKind.Array or JsonValueKind.Null ? value.Clone() : FailConvert(name, "list", out error),
                _ => value.Clone()
            };
        }
        catch (FormatException ex)
        {
            error = Error(RuntimeErrorCode.RuntimeVariableTypeMismatch, $"参数 {name} 类型转换失败：{ex.Message}");
            return null;
        }
    }

    private static JsonElement? ConvertInteger(JsonElement value, string name, out MicroflowRuntimeErrorDto? error)
    {
        error = null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
        {
            return JsonSerializer.SerializeToElement(number, JsonOptions);
        }

        if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
        {
            return JsonSerializer.SerializeToElement(number, JsonOptions);
        }

        return FailConvert(name, "integer", out error);
    }

    private static JsonElement? ConvertDecimal(JsonElement value, string name, out MicroflowRuntimeErrorDto? error)
    {
        error = null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
        {
            return JsonSerializer.SerializeToElement(number, JsonOptions);
        }

        if (value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out number))
        {
            return JsonSerializer.SerializeToElement(number, JsonOptions);
        }

        return FailConvert(name, "decimal", out error);
    }

    private static JsonElement? ConvertBoolean(JsonElement value, string name, out MicroflowRuntimeErrorDto? error)
    {
        error = null;
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return value.Clone();
        }

        if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var boolean))
        {
            return JsonSerializer.SerializeToElement(boolean, JsonOptions);
        }

        return FailConvert(name, "boolean", out error);
    }

    private static JsonElement? ConvertDateTime(JsonElement value, string name, out MicroflowRuntimeErrorDto? error)
    {
        error = null;
        if (value.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dateTime))
        {
            return JsonSerializer.SerializeToElement(dateTime, JsonOptions);
        }

        return FailConvert(name, "dateTime", out error);
    }

    private static JsonElement? FailConvert(string name, string expected, out MicroflowRuntimeErrorDto error)
    {
        error = Error(RuntimeErrorCode.RuntimeVariableTypeMismatch, $"参数 {name} 不能转换为 {expected}。");
        return null;
    }

    private static bool TryReadDefaultValue(JsonElement parameter, out JsonElement value)
    {
        if (parameter.TryGetProperty("defaultValue", out var defaultValue))
        {
            value = defaultValue.Clone();
            return true;
        }

        value = default;
        return false;
    }

    private static string ReadTypeKind(JsonElement type)
    {
        if (type.ValueKind == JsonValueKind.String)
        {
            return NormalizeTypeKind(type.GetString());
        }

        return type.ValueKind == JsonValueKind.Object && type.TryGetProperty("kind", out var kind)
            ? NormalizeTypeKind(kind.GetString())
            : "unknown";
    }

    private static string NormalizeTypeKind(string? kind)
        => (kind ?? "unknown").Trim() switch
        {
            "String" => "string",
            "Integer" or "Int" => "integer",
            "Long" => "long",
            "Decimal" or "Number" => "decimal",
            "Boolean" => "boolean",
            "DateTime" => "dateTime",
            "Object" => "object",
            "List" => "list",
            "Json" => "json",
            var value => value.ToLowerInvariant()
        };

    private static bool ReadBool(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.True;

    private static bool ReadBoolByPath(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var part in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(part, out current))
            {
                return false;
            }
        }

        return current.ValueKind == JsonValueKind.True;
    }

    private static string? ReadString(JsonElement element, string propertyName)
        => MicroflowSchemaReader.ReadString(element, propertyName);

    private static string? ReadStringByPath(JsonElement element, params string[] path)
        => MicroflowSchemaReader.ReadStringByPath(element, path);

    private static string? ReadExpressionText(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return ReadExpressionText(value);
    }

    private static string? ReadExpressionTextByPath(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var part in path[..^1])
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(part, out current))
            {
                return null;
            }
        }

        return ReadExpressionText(current, path[^1]);
    }

    private static string? ReadExpressionText(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString();
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : element.GetRawText();
        }

        return ReadString(element, "raw")
            ?? ReadString(element, "text")
            ?? ReadString(element, "expression");
    }

    private static MicroflowRuntimeErrorDto Error(
        string code,
        string message,
        string? objectId = null,
        string? actionId = null,
        string? flowId = null,
        string? details = null,
        string? microflowId = null,
        IReadOnlyList<string>? callStack = null)
        => new()
        {
            Code = code,
            Message = message,
            ObjectId = objectId,
            ActionId = actionId,
            FlowId = flowId,
            Details = details,
            MicroflowId = microflowId,
            CallStack = callStack
        };

    private static JsonElement Type(string kind)
        => JsonObj(new { kind });

    private static JsonElement JsonObj<T>(T value)
        => JsonSerializer.SerializeToElement(value, JsonOptions);

    private static JsonElement JsonNull()
        => JsonSerializer.SerializeToElement<object?>(null, JsonOptions);

    private static object? ToPlainValue(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.TryGetInt64(out var integer) ? integer : value.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => JsonSerializer.Deserialize<object?>(value.GetRawText(), JsonOptions)
        };

    private sealed class CallExecutionState
    {
    }

    private sealed record ParentCallContext(
        string ParentRunId,
        string RootRunId,
        string CorrelationId,
        int CallDepth,
        IReadOnlyList<string> CallStack,
        string? CallerObjectId,
        string? CallerActionId,
        string ChildRunId);

    private sealed record CallParameterMapping(
        string? TargetParameterName,
        string? TargetParameterId,
        string? Expression,
        string? SourceVariableName,
        string? SourceVariableId);

    private sealed class RuntimeContext
    {
        private readonly MicroflowExecutionRequest _request;
        private readonly IMicroflowClock _clock;
        private readonly IMicroflowTransactionManager? _transactionManager;
        private readonly IMicroflowRuntimeDbSessionFactory? _runtimeDbSessionFactory;
        private readonly IMicroflowVariableStore _expressionVariableStore;
        private RuntimeExecutionContext? _executionContext;
        private MicroflowExecutionPlan? _executionPlan;
        private MicroflowExecutionPlanQuery? _executionPlanQuery;
        private int _steps;
        private bool _sessionFinalized;

        public RuntimeContext(
            MicroflowExecutionRequest request,
            MicroflowSchemaModel model,
            DateTimeOffset startedAt,
            IMicroflowClock clock,
            IMicroflowExpressionEvaluator expressionEvaluator,
            IMicroflowTransactionManager? transactionManager,
            IMicroflowRuntimeDbSessionFactory? runtimeDbSessionFactory,
            string runId,
            string? parentRunId,
            string rootRunId,
            string correlationId,
            int callDepth,
            IReadOnlyList<string> callStackPath,
            string? callerObjectId,
            string? callerActionId,
            IMicroflowVariableStore? variableStore = null,
            string? branchId = null)
        {
            _request = request;
            Model = model;
            StartedAt = startedAt;
            _clock = clock;
            ExpressionEvaluator = expressionEvaluator;
            _transactionManager = transactionManager;
            _runtimeDbSessionFactory = runtimeDbSessionFactory;
            RunId = runId;
            ParentRunId = parentRunId;
            RootRunId = rootRunId;
            CorrelationId = correlationId;
            CallDepth = callDepth;
            CallStackPath = callStackPath;
            CallerObjectId = callerObjectId;
            CallerActionId = callerActionId;
            _expressionVariableStore = variableStore ?? new MicroflowVariableStore();
            BranchId = branchId;
        }

        public MicroflowSchemaModel Model { get; }

        public DateTimeOffset StartedAt { get; }

        public string RunId { get; }

        public string? ParentRunId { get; }

        public string RootRunId { get; }

        public string CorrelationId { get; }

        public int CallDepth { get; }

        public IReadOnlyList<string> CallStackPath { get; }

        public string? CallerObjectId { get; }

        public string? CallerActionId { get; }

        public string? BranchId { get; }

        public IReadOnlyDictionary<string, JsonElement> Input => _request.Input;

        public string ResourceId => _request.ResourceId;

        public string? DebugSessionId => _request.DebugSessionId;

        public int MaxCallDepth => Math.Clamp(_request.MaxCallDepth, 1, 64);

        public MicroflowTestRunOptionsDto Options => _request.Options;

        public MicroflowRequestContext RequestContext => _request.RequestContext;

        public Dictionary<string, RuntimeVariable> Variables { get; } = new(StringComparer.Ordinal);

        public HashSet<string> ModifiedVariableNames { get; } = new(StringComparer.Ordinal);

        public List<MicroflowTraceFrameDto> Frames { get; } = [];

        public List<MicroflowRuntimeLogDto> Logs { get; } = [];

        public List<MicroflowRunSessionDto> ChildRuns { get; } = [];

        public IMicroflowExpressionEvaluator ExpressionEvaluator { get; }

        public IMicroflowVariableStore VariableStore => _expressionVariableStore;

        public MicroflowMetadataCatalogDto? Metadata => _request.Metadata;

        public MicroflowExecutionPlan ResolveExecutionPlan()
        {
            return _executionPlan ??= _request.ExecutionPlan ?? BuildMinimalExecutionPlan();
        }

        public MicroflowExecutionPlanQuery ResolveExecutionPlanQuery()
            => _executionPlanQuery ??= new MicroflowExecutionPlanQuery(ResolveExecutionPlan());

        public MicroflowExecutionNode ResolveExecutionNode(MicroflowObjectModel node, MicroflowActionModel action)
        {
            var plan = ResolveExecutionPlan();
            foreach (var planNode in plan.Nodes)
            {
                if (string.Equals(planNode.ObjectId, node.Id, StringComparison.Ordinal))
                {
                    return planNode;
                }
            }

            return new MicroflowExecutionNode
            {
                ObjectId = node.Id,
                ActionId = action.Id,
                CollectionId = node.CollectionId,
                Kind = node.Kind,
                OfficialType = node.OfficialType,
                Caption = node.Caption,
                ActionKind = action.Kind,
                ActionOfficialType = action.OfficialType,
                SupportLevel = MicroflowRuntimeSupportLevel.Supported,
                ConfigJson = action.Raw
            };
        }

        public MicroflowExecutionNode ResolveLoopExecutionNode(MicroflowObjectModel node)
        {
            var plan = ResolveExecutionPlan();
            foreach (var planNode in plan.Nodes)
            {
                if (string.Equals(planNode.ObjectId, node.Id, StringComparison.Ordinal))
                {
                    return planNode;
                }
            }

            return new MicroflowExecutionNode
            {
                ObjectId = node.Id,
                CollectionId = node.CollectionId,
                Kind = node.Kind,
                OfficialType = node.OfficialType,
                Caption = node.Caption,
                ActionKind = "loopedActivity",
                SupportLevel = MicroflowRuntimeSupportLevel.Supported,
                ConfigJson = node.Raw
            };
        }

        public RuntimeExecutionContext AsRuntimeExecutionContext()
        {
            if (_executionContext is not null)
            {
                return _executionContext;
            }

            var plan = ResolveExecutionPlan();
            var transactionBoundary = NormalizeTransactionBoundary(_request.TransactionBoundary);
            var parentRuntimeContext = _request.ParentRuntimeContext;
            var sharedTransaction = parentRuntimeContext is not null
                && (string.Equals(transactionBoundary, MicroflowCallTransactionBoundary.Inherit, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(transactionBoundary, MicroflowCallTransactionBoundary.SharedTransaction, StringComparison.OrdinalIgnoreCase));
            var disableTransaction = string.Equals(transactionBoundary, MicroflowCallTransactionBoundary.NoTransaction, StringComparison.OrdinalIgnoreCase);
            var transactionManager = disableTransaction
                ? null
                : sharedTransaction
                    ? parentRuntimeContext?.TransactionManager
                    : _transactionManager;
            var transactionOptions = disableTransaction
                ? new MicroflowRuntimeTransactionOptions { Mode = MicroflowRuntimeTransactionMode.None, AutoBegin = false }
                : sharedTransaction
                    ? new MicroflowRuntimeTransactionOptions
                    {
                        Mode = MicroflowRuntimeTransactionMode.SharedInherited,
                        AutoBegin = false,
                        AllowNested = true
                    }
                    : BuildTransactionOptions(transactionBoundary);
            var databaseSession = disableTransaction
                ? null
                : sharedTransaction
                    ? parentRuntimeContext?.DatabaseSession
                    : _runtimeDbSessionFactory?.Create(_request.RequestContext, parentRuntimeContext?.DatabaseSession, transactionBoundary);
            // Reuse the engine's existing variable store so action executors can
            // read variables created by createVariable / createList earlier in the
            // run and write back into the same scope (otherwise loop-aware actions
            // would push iterator scopes onto an empty store).
            _executionContext = RuntimeExecutionContext.Create(
                runId: RunId,
                executionPlan: plan,
                mode: _request.ExecutionMode,
                input: _request.Input,
                securityContext: _request.RequestContext,
                startedAt: StartedAt,
                transactionManager: transactionManager,
                transactionOptions: transactionOptions,
                metadataCatalog: _request.Metadata,
                parentRunId: ParentRunId,
                rootRunId: RootRunId,
                callCorrelationId: CorrelationId,
                maxCallDepth: MaxCallDepth,
                variableStore: _expressionVariableStore,
                debugSessionId: _request.DebugSessionId,
                databaseSession: databaseSession,
                ownsTransactionLifecycle: !sharedTransaction,
                planQuery: ResolveExecutionPlanQuery());
            if (sharedTransaction && parentRuntimeContext is not null)
            {
                _executionContext.Transaction = parentRuntimeContext.Transaction;
                _executionContext.UnitOfWork = parentRuntimeContext.UnitOfWork;
                _executionContext.TransactionManager = parentRuntimeContext.TransactionManager;
                _executionContext.TransactionOptions = parentRuntimeContext.TransactionOptions;
            }
            return _executionContext;
        }

        private MicroflowExecutionPlan BuildMinimalExecutionPlan()
        {
            var nodes = new List<MicroflowExecutionNode>();
            foreach (var obj in Model.Objects)
            {
                nodes.Add(new MicroflowExecutionNode
                {
                    ObjectId = obj.Id,
                    ActionId = obj.Action?.Id,
                    CollectionId = obj.CollectionId,
                    ParentLoopObjectId = obj.ParentLoopObjectId,
                    Kind = obj.Kind,
                    OfficialType = obj.OfficialType,
                    Caption = obj.Caption,
                    ActionKind = obj.Action?.Kind,
                    ActionOfficialType = obj.Action?.OfficialType,
                    SupportLevel = MicroflowRuntimeSupportLevel.Supported,
                    ConfigJson = obj.Action?.Raw ?? obj.Raw
                });
            }

            var flows = Model.Flows.Select(flow => new MicroflowExecutionFlow
            {
                FlowId = flow.Id,
                CollectionId = flow.CollectionId,
                EdgeKind = string.IsNullOrWhiteSpace(flow.EdgeKind) ? "sequence" : flow.EdgeKind!,
                ControlFlow = flow.IsErrorHandler
                    || string.Equals(flow.EdgeKind, "errorHandler", StringComparison.OrdinalIgnoreCase)
                        ? "errorHandler"
                        : string.Equals(flow.EdgeKind, "annotation", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(flow.EdgeKind, "loopBody", StringComparison.OrdinalIgnoreCase)
                            ? "ignored"
                            : "normal",
                OriginObjectId = flow.OriginObjectId,
                DestinationObjectId = flow.DestinationObjectId,
                CaseValues = flow.CaseValues,
                IsErrorHandler = flow.IsErrorHandler,
                BranchOrder = null
            }).ToArray();
            var loopCollections = Model.Objects
                .Where(obj => string.Equals(obj.Kind, "loopedActivity", StringComparison.OrdinalIgnoreCase))
                .Select(loop =>
                {
                    var bodyNodes = Model.Objects.Where(obj => string.Equals(obj.ParentLoopObjectId, loop.Id, StringComparison.Ordinal)).ToArray();
                    var collectionId = bodyNodes.FirstOrDefault()?.CollectionId ?? loop.Id;
                    var bodyFlows = Model.Flows.Where(flow => string.Equals(flow.CollectionId, collectionId, StringComparison.Ordinal)).ToArray();
                    return new MicroflowExecutionLoopCollection
                    {
                        LoopObjectId = loop.Id,
                        CollectionId = collectionId,
                        ParentCollectionId = loop.CollectionId,
                        Nodes = bodyNodes.Select(node => node.Id).ToArray(),
                        Flows = bodyFlows.Select(flow => flow.Id).ToArray(),
                        StartLikeNodeIds = bodyNodes
                            .Where(node => !string.Equals(node.Kind, "annotation", StringComparison.OrdinalIgnoreCase)
                                && !string.Equals(node.Kind, "parameterObject", StringComparison.OrdinalIgnoreCase))
                            .Select(node => node.Id)
                            .Take(1)
                            .ToArray(),
                        TerminalNodeIds = bodyNodes
                            .Where(node => node.Kind is "endEvent" or "errorEvent" or "breakEvent" or "continueEvent")
                            .Select(node => node.Id)
                            .ToArray()
                    };
                })
                .ToArray();

            return new MicroflowExecutionPlan
            {
                Id = ResourceId ?? Guid.NewGuid().ToString("N"),
                SchemaId = _request.SchemaId,
                ResourceId = ResourceId,
                Version = _request.Version,
                SchemaVersion = Model.SchemaVersion,
                StartNodeId = Model.Objects.FirstOrDefault(obj => string.Equals(obj.Kind, "startEvent", StringComparison.OrdinalIgnoreCase))?.Id ?? string.Empty,
                EndNodeIds = Model.Objects.Where(obj => string.Equals(obj.Kind, "endEvent", StringComparison.OrdinalIgnoreCase)).Select(obj => obj.Id).ToArray(),
                Nodes = nodes,
                Flows = flows,
                NormalFlows = flows.Where(flow => string.Equals(flow.ControlFlow, "normal", StringComparison.OrdinalIgnoreCase)).ToArray(),
                ErrorHandlerFlows = flows.Where(flow => string.Equals(flow.ControlFlow, "errorHandler", StringComparison.OrdinalIgnoreCase)).ToArray(),
                IgnoredFlows = flows.Where(flow => string.Equals(flow.ControlFlow, "ignored", StringComparison.OrdinalIgnoreCase)).ToArray(),
                LoopCollections = loopCollections,
                Parameters = Model.Parameters
                    .Select(parameter => new MicroflowExecutionParameter
                    {
                        Id = parameter.Id,
                        Name = parameter.Name,
                        DataTypeJson = parameter.Type,
                        Required = ReadBool(parameter.Raw, "required")
                    })
                    .ToArray()
            };
        }

        public bool TryStep(out MicroflowRuntimeErrorDto error)
        {
            _steps++;
            var maxSteps = Math.Clamp(_request.Options.MaxSteps ?? 1000, 1, 5000);
            if (_steps <= maxSteps)
            {
                error = new MicroflowRuntimeErrorDto();
                return true;
            }

            error = Error(RuntimeErrorCode.RuntimeMaxStepsExceeded, $"Microflow runtime exceeded maxSteps={maxSteps}.");
            return false;
        }

        public void SetVariable(string name, JsonElement type, JsonElement value, string source)
        {
            Variables[name] = new RuntimeVariable(name, type.Clone(), value.Clone(), source);
            ModifiedVariableNames.Add(name);
            var rawValueJson = value.GetRawText();
            var definition = new MicroflowVariableDefinition
            {
                Name = name,
                DataTypeJson = type.GetRawText(),
                RawValueJson = rawValueJson,
                ValuePreview = Preview(value),
                SourceKind = source,
                ScopeKind = source == "parameter" ? MicroflowVariableScopeKind.Global : MicroflowVariableScopeKind.Action,
                AllowShadowing = true
            };
            if (_expressionVariableStore.Exists(name))
            {
                _expressionVariableStore.Set(
                    name,
                    new MicroflowRuntimeVariableValue
                    {
                        Name = name,
                        DataTypeJson = definition.DataTypeJson,
                        RawValueJson = rawValueJson,
                        ValuePreview = definition.ValuePreview,
                        SourceKind = source,
                        ScopeKind = definition.ScopeKind
                    });
                return;
            }

            _expressionVariableStore.Define(definition);
        }

        public RuntimeContext ForkForBranch(string branchId, IVariableScopeForker variableScopeForker)
        {
            var branchRequest = _request with
            {
                ParentRuntimeContext = AsRuntimeExecutionContext(),
                TransactionBoundary = MicroflowCallTransactionBoundary.SharedTransaction
            };
            var branchStore = variableScopeForker.Fork(_expressionVariableStore, branchId);
            var branchContext = new RuntimeContext(
                branchRequest,
                Model,
                StartedAt,
                _clock,
                ExpressionEvaluator,
                _transactionManager,
                _runtimeDbSessionFactory,
                RunId,
                ParentRunId,
                RootRunId,
                CorrelationId,
                CallDepth,
                CallStackPath,
                CallerObjectId,
                CallerActionId,
                branchStore,
                branchId);
            foreach (var pair in Variables)
            {
                branchContext.Variables[pair.Key] = new RuntimeVariable(pair.Value.Name, pair.Value.Type.Clone(), pair.Value.Value.Clone(), pair.Value.Source);
            }

            branchContext._executionPlan = _executionPlan;
            branchContext._executionPlanQuery = _executionPlanQuery;
            return branchContext;
        }

        public IReadOnlyList<BranchWriteIntent> CollectWriteIntents(string branchId)
            => ModifiedVariableNames
                .Select(name => new BranchWriteIntent
                {
                    BranchId = branchId,
                    VariableName = name
                })
                .ToArray();

        public void MergeFromBranch(RuntimeContext branchContext)
        {
            foreach (var variableName in branchContext.ModifiedVariableNames)
            {
                if (!branchContext.VariableStore.TryGet(variableName, out var value) || value is null)
                {
                    continue;
                }

                ApplyVariableValue(variableName, value);
            }

            Frames.AddRange(branchContext.Frames);
            Logs.AddRange(branchContext.Logs);
        }

        private void ApplyVariableValue(string name, MicroflowRuntimeVariableValue value)
        {
            if (_expressionVariableStore.Exists(name))
            {
                _expressionVariableStore.Set(name, value with { Name = name });
            }
            else
            {
                _expressionVariableStore.Define(new MicroflowVariableDefinition
                {
                    Name = name,
                    Value = value with { Name = name },
                    DataTypeJson = value.DataTypeJson,
                    ScopeKind = value.ScopeKind,
                    AllowShadowing = true
                });
            }

            Variables[name] = new RuntimeVariable(
                name,
                MicroflowVariableStore.ToJsonElement(value.DataTypeJson) ?? JsonNull(),
                MicroflowVariableStore.ToJsonElement(value.RawValueJson) ?? JsonNull(),
                value.SourceKind);
        }

        public JsonElement EvaluateExpression(string expression, string? currentObjectId = null, string? currentActionId = null, string? currentFlowId = null)
        {
            var result = EvaluateExpressionCore(expression, currentObjectId, currentActionId, currentFlowId);
            if (!result.Success)
            {
                var normalizedExpression = NormalizeBareVariableExpression(expression);
                if (!string.Equals(normalizedExpression, expression, StringComparison.Ordinal))
                {
                    result = EvaluateExpressionCore(normalizedExpression, currentObjectId, currentActionId, currentFlowId);
                }
            }

            if (!result.Success || result.RawValueJson is null)
            {
                throw new RuntimeExpressionException(Error(
                    result.Error?.Code ?? RuntimeErrorCode.RuntimeExpressionError,
                    result.Error?.Message ?? "表达式求值失败。",
                    currentObjectId,
                    currentActionId,
                    currentFlowId,
                    details: result.Error?.Details));
            }

            return MicroflowVariableStore.ToJsonElement(result.RawValueJson) ?? JsonNull();
        }

        private MicroflowExpressionEvaluationResult EvaluateExpressionCore(
            string expression,
            string? currentObjectId,
            string? currentActionId,
            string? currentFlowId)
            => ExpressionEvaluator.Evaluate(
                expression,
                new MicroflowExpressionEvaluationContext
                {
                    RuntimeExecutionContext = null!,
                    VariableStore = _expressionVariableStore,
                    MetadataCatalog = _request.Metadata,
                    CurrentObjectId = currentObjectId,
                    CurrentActionId = currentActionId,
                    CurrentFlowId = currentFlowId,
                    Mode = MicroflowRuntimeExecutionMode.TestRun,
                    Options = new MicroflowExpressionEvaluationOptions
                    {
                        AllowUnknownVariables = false,
                        AllowUnsupportedFunctions = false,
                        StrictTypeCheck = true,
                        MaxEvaluationDepth = 64,
                        MaxStringLength = 500
                    }
                });

        private string NormalizeBareVariableExpression(string expression)
        {
            var normalized = expression;
            foreach (var name in Variables.Keys.OrderByDescending(static item => item.Length))
            {
                normalized = System.Text.RegularExpressions.Regex.Replace(
                    normalized,
                    $@"(?<![\$\w]){System.Text.RegularExpressions.Regex.Escape(name)}(?![\w])",
                    $"${name}");
            }

            return normalized;
        }

        public void AddChildRun(MicroflowRunSessionDto childRun)
        {
            ChildRuns.Add(childRun);
        }

        public void AddNodeFailure(MicroflowObjectModel node, string? incomingFlowId, MicroflowRuntimeErrorDto error)
            => AddFrame(node, incomingFlowId, null, "failed", JsonObj(new { node.Kind, actionKind = node.Action?.Kind }), null, error);

        public void AddNodeFailure(
            MicroflowObjectModel node,
            string? incomingFlowId,
            MicroflowRuntimeErrorDto error,
            string? outgoingFlowId)
            => AddFrame(
                node,
                incomingFlowId,
                outgoingFlowId,
                "failed",
                JsonObj(new { node.Kind, actionKind = node.Action?.Kind }),
                null,
                error,
                message: outgoingFlowId is null ? null : "Routed to error handler flow.");

        public void AddFrame(
            MicroflowObjectModel node,
            string? incomingFlowId,
            string? outgoingFlowId,
            string status,
            JsonElement? input,
            JsonElement? output,
            MicroflowRuntimeErrorDto? error,
            string? message = null)
            => AddFrame(node.Id, node.Caption ?? node.Id, node.Kind, node.Action?.Id, incomingFlowId, outgoingFlowId, status, input, output, error, message: message);

        public void AddFrame(
            MicroflowObjectModel node,
            string? incomingFlowId,
            string? outgoingFlowId,
            string status,
            JsonElement? input,
            JsonElement? output,
            MicroflowRuntimeErrorDto? error,
            JsonElement? selectedCaseValue)
            => AddFrame(node.Id, node.Caption ?? node.Id, node.Kind, node.Action?.Id, incomingFlowId, outgoingFlowId, status, input, output, error, selectedCaseValue);

        public void AddFrame(
            string objectId,
            string objectTitle,
            string kind,
            string? actionId,
            string? incomingFlowId,
            string? outgoingFlowId,
            string status,
            JsonElement? input,
            JsonElement? output,
            MicroflowRuntimeErrorDto? error,
            JsonElement? selectedCaseValue = null,
            string? message = null)
        {
            var started = _clock.UtcNow;
            var ended = _clock.UtcNow;
            var preparedInput = PrepareTracePayload(input);
            var preparedOutput = PrepareTracePayload(output);
            Frames.Add(new MicroflowTraceFrameDto
            {
                Id = Guid.NewGuid().ToString("N"),
                RunId = RunId,
                MicroflowId = ResourceId,
                ParentRunId = ParentRunId,
                RootRunId = RootRunId,
                CallDepth = CallDepth,
                CallerObjectId = CallerObjectId,
                CallerActionId = CallerActionId,
                ObjectId = objectId,
                ActionId = actionId,
                IncomingFlowId = incomingFlowId,
                OutgoingFlowId = outgoingFlowId,
                SelectedCaseValue = selectedCaseValue,
                Status = status,
                StartedAt = started,
                EndedAt = ended,
                DurationMs = Math.Max(0, (int)(ended - started).TotalMilliseconds),
                Input = preparedInput,
                Output = preparedOutput,
                Error = error,
                VariablesSnapshot = SnapshotVariables(),
                Message = message ?? objectTitle
            });
            AsRuntimeExecutionContext().NodeResults[objectId] = new NodeExecutionResultSummary
            {
                ObjectId = objectId,
                ActionId = actionId,
                Status = status,
                DurationMs = Math.Max(0, (int)(ended - started).TotalMilliseconds),
                OutgoingFlowId = outgoingFlowId,
                Message = message ?? objectTitle,
                Output = preparedOutput
            };
        }

        public MicroflowRunSessionDto BuildSession(string status, MicroflowRuntimeErrorDto? error, JsonElement? output = null)
        {
            FinalizeTransaction(status, error);
            var endedAt = _clock.UtcNow;
            if (error is not null)
            {
                error = error with
                {
                    MicroflowId = error.MicroflowId ?? ResourceId,
                    CallStack = error.CallStack ?? CallStackPath
                };
            }

            return new MicroflowRunSessionDto
            {
                Id = RunId,
                SchemaId = _request.SchemaId,
                ResourceId = _request.ResourceId,
                Version = _request.Version,
                ParentRunId = ParentRunId,
                RootRunId = RootRunId,
                CallDepth = CallDepth,
                CorrelationId = CorrelationId,
                CallStack = CallStackPath,
                StartedAt = StartedAt,
                EndedAt = endedAt,
                Status = status,
                Input = _request.Input,
                Output = string.Equals(status, "success", StringComparison.OrdinalIgnoreCase) ? output : null,
                Error = error,
                Trace = Frames,
                Logs = Logs,
                Variables = Frames.Select(frame => new MicroflowVariableSnapshotDto
                {
                    FrameId = frame.Id,
                    ObjectId = frame.ObjectId,
                    Variables = frame.VariablesSnapshot?.Values.ToArray() ?? Array.Empty<MicroflowRuntimeVariableValueDto>()
                }).ToArray(),
                TransactionSummary = ToTransactionSummary(_executionContext?.CreateTransactionSnapshot("session")),
                ErrorHandlingSummary = _executionContext?.ErrorHandlingSummary,
                ChildRuns = ChildRuns,
                ChildRunIds = ChildRuns.Select(child => child.Id).ToArray()
            };
        }

        private Dictionary<string, MicroflowRuntimeVariableValueDto> SnapshotVariables()
        {
            var snapshot = AsRuntimeExecutionContext().CreateSnapshot(
                objectId: null,
                actionId: null,
                collectionId: null,
                stepIndex: _steps,
                includeSystem: true,
                includeRawValue: true,
                maxRawValueLength: 4096);
            return snapshot.Variables.ToDictionary(
                pair => pair.Key,
                pair => new MicroflowRuntimeVariableValueDto
                {
                    Name = pair.Value.Name,
                    Type = pair.Value.DataTypeJson is null ? null : MicroflowVariableStore.ToJsonElement(pair.Value.DataTypeJson),
                    ValuePreview = pair.Value.ValuePreview,
                    RawValue = pair.Value.RawValueJson is null ? null : MicroflowVariableStore.ToJsonElement(pair.Value.RawValueJson),
                    RawValueJson = pair.Value.RawValueJson,
                    Source = pair.Value.SourceKind,
                    SourceObjectId = pair.Value.SourceObjectId,
                    SourceActionId = pair.Value.SourceActionId,
                    CollectionId = pair.Value.CollectionId,
                    Readonly = pair.Value.Readonly,
                    ScopeKind = pair.Value.ScopeKind,
                    EstimatedSizeBytes = pair.Value.EstimatedSizeBytes,
                    IsLargeObject = pair.Value.IsLargeObject,
                    ValueRef = pair.Value.ValueRef
                },
                StringComparer.Ordinal);
        }

        private JsonElement? PrepareTracePayload(JsonElement? payload)
        {
            if (!payload.HasValue)
            {
                return null;
            }

            var runtimeContext = AsRuntimeExecutionContext();
            if (runtimeContext.ShouldCaptureRawValues())
            {
                return payload;
            }

            var raw = payload.Value.GetRawText();
            var sizeBytes = MicroflowVariableStore.EstimateSizeBytes(raw);
            if (sizeBytes <= runtimeContext.MemoryBudget.MaxNodeOutputBytes)
            {
                return payload;
            }

            return JsonObj(new
            {
                summary = true,
                sizeBytes,
                preview = MicroflowVariableStore.TrimPreview(MicroflowVariableStore.Preview(raw), 200)
            });
        }

        public MicroflowDebugRuntimeSnapshot CreateDebugSnapshot(MicroflowDebugSafePoint point)
        {
            var variables = SnapshotVariables()
                .Values
                .Select(variable => new DebugVariableSnapshot
                {
                    Name = variable.Name,
                    Type = variable.Type is { } variableType
                        && variableType.ValueKind == JsonValueKind.Object
                        && variableType.TryGetProperty("kind", out var kind)
                        ? kind.GetString() ?? "unknown"
                        : "unknown",
                    ValuePreview = variable.ValuePreview,
                    RawValueJson = variable.RawValueJson,
                    ScopeKind = variable.ScopeKind ?? "local",
                    ObjectId = point.NodeObjectId,
                    FlowId = point.IncomingFlowId,
                    BranchId = point.BranchId
                })
                .ToArray();

            return new MicroflowDebugRuntimeSnapshot
            {
                ResourceId = ResourceId,
                ParentRunId = ParentRunId,
                RootRunId = RootRunId,
                CallDepth = CallDepth,
                CallStack = CallStackPath,
                Variables = variables
            };
        }

        private void FinalizeTransaction(string status, MicroflowRuntimeErrorDto? error)
        {
            if (_sessionFinalized || _executionContext is null || !_executionContext.OwnsTransactionLifecycle)
            {
                return;
            }

            _sessionFinalized = true;
            if (_executionContext.TransactionManager is null || _executionContext.Transaction is null)
            {
                return;
            }

            if (!string.Equals(_executionContext.Transaction.Status, MicroflowRuntimeTransactionStatus.Active, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (string.Equals(status, "success", StringComparison.OrdinalIgnoreCase))
            {
                _executionContext.TransactionManager.Commit(_executionContext, "run completed");
                return;
            }

            _executionContext.TransactionManager.Rollback(_executionContext, "run failed", error);
        }

        private static MicroflowRuntimeTransactionOptions BuildTransactionOptions(string transactionBoundary)
            => string.Equals(transactionBoundary, MicroflowCallTransactionBoundary.ChildTransaction, StringComparison.OrdinalIgnoreCase)
                ? new MicroflowRuntimeTransactionOptions
                {
                    Mode = MicroflowRuntimeTransactionMode.ChildTransaction,
                    AutoBegin = true,
                    AllowNested = true
                }
                : new MicroflowRuntimeTransactionOptions();

        private static string NormalizeTransactionBoundary(string? boundary)
            => boundary switch
            {
                MicroflowCallTransactionBoundary.SharedTransaction => MicroflowCallTransactionBoundary.SharedTransaction,
                MicroflowCallTransactionBoundary.ChildTransaction => MicroflowCallTransactionBoundary.ChildTransaction,
                MicroflowCallTransactionBoundary.NoTransaction => MicroflowCallTransactionBoundary.NoTransaction,
                _ => MicroflowCallTransactionBoundary.Inherit
            };

        private static Runtime.Transactions.MicroflowRuntimeTransactionSummary? ToTransactionSummary(MicroflowRuntimeTransactionSnapshot? snapshot)
            => snapshot is null
                ? null
                : new Runtime.Transactions.MicroflowRuntimeTransactionSummary
                {
                    TransactionId = snapshot.TransactionId,
                    Status = snapshot.Status,
                    ChangedObjectCount = snapshot.ChangedObjectCount,
                    CommittedObjectCount = snapshot.CommittedObjectCount,
                    RolledBackObjectCount = snapshot.RolledBackObjectCount,
                    LogCount = snapshot.LogCount,
                    DiagnosticsCount = snapshot.DiagnosticsCount
                };
    }

    private sealed record RuntimeVariable(string Name, JsonElement Type, JsonElement Value, string Source);

    private sealed record NodeExecution(bool Success, bool Completed, string? NextNodeId, string? OutgoingFlowId, JsonElement? Output, MicroflowRuntimeErrorDto? Error)
    {
        public static NodeExecution Next(string nextNodeId, string outgoingFlowId) => new(true, false, nextNodeId, outgoingFlowId, null, null);

        public static NodeExecution Done(JsonElement output) => new(true, true, null, null, output, null);

        public static NodeExecution Failed(MicroflowRuntimeErrorDto error) => new(false, false, null, null, null, error);
    }

    private sealed class MicroflowRuntimeGraph
    {
        private readonly string? _startNodeId;
        private readonly Dictionary<string, IReadOnlyList<MicroflowFlowModel>> _normalOutgoingByObjectId;
        private readonly Dictionary<string, IReadOnlyList<MicroflowFlowModel>> _errorOutgoingByObjectId;

        private MicroflowRuntimeGraph(
            IReadOnlyDictionary<string, MicroflowObjectModel> objects,
            IReadOnlyDictionary<string, MicroflowFlowModel> flows,
            string? startNodeId = null,
            Dictionary<string, IReadOnlyList<MicroflowFlowModel>>? normalOutgoingByObjectId = null,
            Dictionary<string, IReadOnlyList<MicroflowFlowModel>>? errorOutgoingByObjectId = null)
        {
            Objects = objects;
            Flows = flows;
            _startNodeId = startNodeId;
            _normalOutgoingByObjectId = normalOutgoingByObjectId ?? [];
            _errorOutgoingByObjectId = errorOutgoingByObjectId ?? [];
        }

        public IReadOnlyDictionary<string, MicroflowObjectModel> Objects { get; }

        public IReadOnlyDictionary<string, MicroflowFlowModel> Flows { get; }

        public static MicroflowRuntimeGraph Build(MicroflowSchemaModel model)
        {
            var objects = model.Objects.Where(static item => !string.IsNullOrWhiteSpace(item.Id)).ToDictionary(static item => item.Id, StringComparer.Ordinal);
            var flows = model.Flows.Where(static item => !string.IsNullOrWhiteSpace(item.Id)).ToDictionary(static item => item.Id, StringComparer.Ordinal);
            foreach (var flow in flows.Values)
            {
                if (string.IsNullOrWhiteSpace(flow.OriginObjectId) || !objects.ContainsKey(flow.OriginObjectId))
                {
                    throw new RuntimeExpressionException(Error(RuntimeErrorCode.RuntimeFlowNotFound, $"Sequence flow source 不存在：{flow.Id}", flowId: flow.Id));
                }

                if (string.IsNullOrWhiteSpace(flow.DestinationObjectId) || !objects.ContainsKey(flow.DestinationObjectId))
                {
                    throw new RuntimeExpressionException(Error(RuntimeErrorCode.RuntimeFlowNotFound, $"Sequence flow target 不存在：{flow.Id}", flow.OriginObjectId, flowId: flow.Id));
                }
            }

            return new MicroflowRuntimeGraph(objects, flows);
        }

        public static MicroflowRuntimeGraph Build(
            MicroflowSchemaModel model,
            MicroflowExecutionPlan plan,
            MicroflowExecutionPlanQuery query)
        {
            var objects = model.Objects
                .Where(static item => !string.IsNullOrWhiteSpace(item.Id))
                .ToDictionary(static item => item.Id, StringComparer.Ordinal);
            var modelFlows = model.Flows
                .Where(static item => !string.IsNullOrWhiteSpace(item.Id))
                .ToDictionary(static item => item.Id, StringComparer.Ordinal);
            var flows = new Dictionary<string, MicroflowFlowModel>(StringComparer.Ordinal);
            foreach (var planFlow in plan.Flows)
            {
                if (string.IsNullOrWhiteSpace(planFlow.FlowId))
                {
                    continue;
                }

                if (modelFlows.TryGetValue(planFlow.FlowId, out var modelFlow))
                {
                    flows[planFlow.FlowId] = modelFlow with
                    {
                        OriginObjectId = planFlow.OriginObjectId,
                        DestinationObjectId = planFlow.DestinationObjectId,
                        IsErrorHandler = planFlow.IsErrorHandler,
                        CaseValues = planFlow.CaseValues
                    };
                    continue;
                }

                flows[planFlow.FlowId] = new MicroflowFlowModel
                {
                    Id = planFlow.FlowId,
                    Kind = "sequence",
                    OriginObjectId = planFlow.OriginObjectId,
                    DestinationObjectId = planFlow.DestinationObjectId,
                    EdgeKind = planFlow.EdgeKind,
                    IsErrorHandler = planFlow.IsErrorHandler,
                    CaseValues = planFlow.CaseValues,
                    CollectionId = planFlow.CollectionId ?? string.Empty,
                    Raw = JsonNull()
                };
            }

            foreach (var flow in flows.Values)
            {
                if (string.IsNullOrWhiteSpace(flow.OriginObjectId) || !objects.ContainsKey(flow.OriginObjectId))
                {
                    throw new RuntimeExpressionException(Error(RuntimeErrorCode.RuntimeFlowNotFound, $"Sequence flow source 不存在：{flow.Id}", flowId: flow.Id));
                }

                if (string.IsNullOrWhiteSpace(flow.DestinationObjectId) || !objects.ContainsKey(flow.DestinationObjectId))
                {
                    throw new RuntimeExpressionException(Error(RuntimeErrorCode.RuntimeFlowNotFound, $"Sequence flow target 不存在：{flow.Id}", flow.OriginObjectId, flowId: flow.Id));
                }
            }

            var normalOutgoing = plan.Nodes
                .Where(node => !string.IsNullOrWhiteSpace(node.ObjectId))
                .ToDictionary(
                    node => node.ObjectId,
                    node => (IReadOnlyList<MicroflowFlowModel>)query.GetNormalOutgoingFlows(plan, node.ObjectId, node.CollectionId)
                        .Concat(query.GetDecisionOutgoingFlows(plan, node.ObjectId, node.CollectionId))
                        .Concat(query.GetObjectTypeOutgoingFlows(plan, node.ObjectId, node.CollectionId))
                        .Select(flow => flows[flow.FlowId])
                        .ToArray(),
                    StringComparer.Ordinal);
            var errorOutgoing = plan.Nodes
                .Where(node => !string.IsNullOrWhiteSpace(node.ObjectId))
                .ToDictionary(
                    node => node.ObjectId,
                    node => (IReadOnlyList<MicroflowFlowModel>)query.GetErrorHandlerFlows(plan, node.ObjectId, node.CollectionId)
                        .Select(flow => flows[flow.FlowId])
                        .ToArray(),
                    StringComparer.Ordinal);

            return new MicroflowRuntimeGraph(objects, flows, plan.StartNodeId, normalOutgoing, errorOutgoing);
        }

        public StartResult FindStart()
        {
            if (!string.IsNullOrWhiteSpace(_startNodeId) && Objects.TryGetValue(_startNodeId, out var plannedStart))
            {
                return StartResult.Ok(plannedStart);
            }

            var starts = Objects.Values.Where(static node => string.Equals(node.Kind, "startEvent", StringComparison.OrdinalIgnoreCase) && !node.InsideLoop).ToArray();
            return starts.Length switch
            {
                1 => StartResult.Ok(starts[0]),
                0 => StartResult.Fail(Error(RuntimeErrorCode.RuntimeStartNotFound, "未找到 Start 节点。")),
                _ => StartResult.Fail(Error(RuntimeErrorCode.RuntimeStartNotFound, $"找到多个 Start 节点：{starts.Length}。"))
            };
        }

        public IReadOnlyList<MicroflowFlowModel> NormalOutgoing(string objectId)
            => _normalOutgoingByObjectId.TryGetValue(objectId, out var planned)
                ? planned
                : Flows.Values
                .Where(flow => string.Equals(flow.OriginObjectId, objectId, StringComparison.Ordinal)
                    && !flow.IsErrorHandler
                    && !string.Equals(flow.Kind, "annotation", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(flow.EdgeKind, "annotation", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(flow.EdgeKind, "loopBody", StringComparison.OrdinalIgnoreCase))
                .ToArray();

        /// <summary>
        /// Returns the <see cref="MicroflowFlowModel"/>(s) emanating from <paramref name="objectId"/>
        /// that are explicitly marked as error-handler edges (used by P0-4 error routing).
        /// </summary>
        public IReadOnlyList<MicroflowFlowModel> ErrorHandlerOutgoing(string objectId)
            => _errorOutgoingByObjectId.TryGetValue(objectId, out var planned)
                ? planned
                : Flows.Values
                .Where(flow => string.Equals(flow.OriginObjectId, objectId, StringComparison.Ordinal)
                    && flow.IsErrorHandler
                    && !string.Equals(flow.Kind, "annotation", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(flow.EdgeKind, "annotation", StringComparison.OrdinalIgnoreCase))
                .ToArray();

        public CaseFlowResult SelectBooleanCaseFlow(string objectId, bool value)
        {
            var expected = value ? "true" : "false";
            var matches = NormalOutgoing(objectId)
                .Select(flow => new { Flow = flow, Case = flow.CaseValues.FirstOrDefault(caseValue => CaseMatches(caseValue, expected)) })
                .Where(item => item.Case.ValueKind != JsonValueKind.Undefined)
                .ToArray();
            if (matches.Length != 1)
            {
                return CaseFlowResult.Fail(Error(RuntimeErrorCode.RuntimeInvalidCase, $"Decision 分支 {expected} 匹配数量不正确：{matches.Length}", objectId));
            }

            return CaseFlowResult.Ok(matches[0].Flow, matches[0].Case.Clone());
        }

        public static bool CaseMatches(JsonElement caseValue, string expected)
        {
            if (caseValue.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                return string.Equals(caseValue.GetBoolean().ToString(), expected, StringComparison.OrdinalIgnoreCase);
            }

            if (caseValue.ValueKind == JsonValueKind.Object
                && caseValue.TryGetProperty("value", out var booleanValue)
                && booleanValue.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                return string.Equals(booleanValue.GetBoolean().ToString(), expected, StringComparison.OrdinalIgnoreCase);
            }

            var value = ReadString(caseValue, "persistedValue")
                ?? ReadString(caseValue, "value")
                ?? ReadString(caseValue, "conditionKey")
                ?? ReadString(caseValue, "kind")
                ?? (caseValue.ValueKind == JsonValueKind.String ? caseValue.GetString() : null);
            return string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
        }

        public Dictionary<string, int> ComputeDistances(string startNodeId, string? stopAtNodeId = null)
        {
            var distances = new Dictionary<string, int>(StringComparer.Ordinal);
            var queue = new Queue<(string NodeId, int Distance)>();
            queue.Enqueue((startNodeId, 0));
            while (queue.Count > 0)
            {
                var (nodeId, distance) = queue.Dequeue();
                if (distances.TryGetValue(nodeId, out var existing) && existing <= distance)
                {
                    continue;
                }

                distances[nodeId] = distance;
                if (!string.IsNullOrWhiteSpace(stopAtNodeId) && string.Equals(nodeId, stopAtNodeId, StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var flow in NormalOutgoing(nodeId))
                {
                    if (!string.IsNullOrWhiteSpace(flow.DestinationObjectId))
                    {
                        queue.Enqueue((flow.DestinationObjectId!, distance + 1));
                    }
                }
            }

            return distances;
        }
    }

    private sealed record StartResult(bool Success, MicroflowObjectModel? Object, MicroflowRuntimeErrorDto? Error)
    {
        public static StartResult Ok(MicroflowObjectModel obj) => new(true, obj, null);

        public static StartResult Fail(MicroflowRuntimeErrorDto error) => new(false, null, error);
    }

    private sealed record CaseFlowResult(bool Success, MicroflowFlowModel? Flow, JsonElement? CaseValue, MicroflowRuntimeErrorDto? Error)
    {
        public static CaseFlowResult Ok(MicroflowFlowModel flow, JsonElement caseValue) => new(true, flow, caseValue, null);

        public static CaseFlowResult Fail(MicroflowRuntimeErrorDto error) => new(false, null, null, error);
    }

    private sealed class RuntimeExpressionException : Exception
    {
        public RuntimeExpressionException(MicroflowRuntimeErrorDto error)
            : base(error.Message)
        {
            Error = error;
        }

        public MicroflowRuntimeErrorDto Error { get; }
    }

    private static string Preview(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.GetRawText(),
            JsonValueKind.Null or JsonValueKind.Undefined => "null",
            _ => value.GetRawText().Length > 120 ? string.Concat(value.GetRawText().AsSpan(0, 120), "...") : value.GetRawText()
        };
}
