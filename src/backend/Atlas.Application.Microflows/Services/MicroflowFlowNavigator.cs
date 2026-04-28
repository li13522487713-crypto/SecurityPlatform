using System.Text.Json;
using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Runtime.Actions;
using Atlas.Application.Microflows.Runtime.Expressions;
using Atlas.Application.Microflows.Runtime.Loops;

namespace Atlas.Application.Microflows.Services;

public sealed class MicroflowFlowNavigator : IMicroflowFlowNavigator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IMicroflowClock _clock;
    private readonly IMicroflowExpressionEvaluator _expressionEvaluator;
    private readonly IMicroflowActionExecutorRegistry _actionExecutorRegistry;
    private readonly IMicroflowLoopExecutor _loopExecutor;
    private readonly IMicroflowRuntimeConnectorRegistry _connectorRegistry;

    public MicroflowFlowNavigator(
        IMicroflowClock clock,
        IMicroflowExpressionEvaluator expressionEvaluator,
        IMicroflowActionExecutorRegistry actionExecutorRegistry,
        IMicroflowLoopExecutor loopExecutor,
        IMicroflowRuntimeConnectorRegistry connectorRegistry)
    {
        _clock = clock;
        _expressionEvaluator = expressionEvaluator;
        _actionExecutorRegistry = actionExecutorRegistry;
        _loopExecutor = loopExecutor;
        _connectorRegistry = connectorRegistry;
    }

    public Task<MicroflowNavigationResult> NavigateAsync(
        MicroflowExecutionPlan plan,
        MicroflowNavigationOptions options,
        CancellationToken cancellationToken)
    {
        var effectiveOptions = options ?? new MicroflowNavigationOptions();
        var startedAt = _clock.UtcNow;
        var context = new MicroflowNavigationContext(
            plan,
            effectiveOptions,
            Guid.NewGuid().ToString("N"),
            string.IsNullOrWhiteSpace(effectiveOptions.TraceId) ? Guid.NewGuid().ToString("N") : effectiveOptions.TraceId!,
            cancellationToken);
        var query = new MicroflowExecutionPlanQuery(plan);

        NavigationSignal signal;
        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                signal = NavigationSignal.Cancelled(CancelledError());
            }
            else
            {
                var startNodeId = string.IsNullOrWhiteSpace(effectiveOptions.PreferredStartNodeId)
                    ? plan.StartNodeId
                    : effectiveOptions.PreferredStartNodeId!;
                if (string.IsNullOrWhiteSpace(startNodeId) || !query.TryGetNode(plan, startNodeId, out _))
                {
                    signal = NavigationSignal.Failed(Error(
                        RuntimeErrorCode.RuntimeStartNotFound,
                        "ExecutionPlan start node is missing.",
                        objectId: startNodeId));
                }
                else
                {
                    signal = NavigatePath(context, query, startNodeId, incomingFlowId: null, collectionId: query.GetNode(plan, startNodeId).CollectionId, loopIteration: null);
                    if (signal.Kind is NavigationSignalKind.Break or NavigationSignalKind.Continue)
                    {
                        signal = NavigationSignal.Failed(Error(
                            RuntimeErrorCode.RuntimeLoopControlOutOfScope,
                            "BreakEvent or ContinueEvent reached outside loop context.",
                            objectId: context.CurrentNodeId,
                            flowId: context.CurrentFlowId));
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            signal = NavigationSignal.Cancelled(CancelledError());
        }
        catch (Exception ex)
        {
            signal = NavigationSignal.Failed(Error(RuntimeErrorCode.RuntimeUnknownError, "FlowNavigator failed.", details: ex.Message));
        }

        var endedAt = _clock.UtcNow;
        var status = signal.Kind switch
        {
            NavigationSignalKind.Success => MicroflowNavigationStatus.Success,
            NavigationSignalKind.Cancelled => MicroflowNavigationStatus.Cancelled,
            NavigationSignalKind.MaxStepsExceeded => MicroflowNavigationStatus.MaxStepsExceeded,
            _ => MicroflowNavigationStatus.Failed
        };
        var diagnostics = effectiveOptions.IncludeDiagnostics
            ? context.Diagnostics.Concat(plan.Diagnostics).Concat(context.RuntimeContext.Diagnostics.Select(ToExecutionDiagnostic)).ToArray()
            : Array.Empty<MicroflowExecutionDiagnosticDto>();
        var result = new MicroflowNavigationResult
        {
            RunId = context.RunId,
            TraceId = context.TraceId,
            Status = status,
            StartedAt = startedAt,
            EndedAt = endedAt,
            DurationMs = DurationMs(startedAt, endedAt),
            Steps = context.Steps,
            TraceFrames = context.Steps.Select(ToTraceFrame).ToArray(),
            Diagnostics = new MicroflowFlowNavigatorDiagnostics
            {
                Items = diagnostics,
                ErrorCount = diagnostics.Count(item => string.Equals(item.Severity, "error", StringComparison.OrdinalIgnoreCase)),
                WarningCount = diagnostics.Count(item => string.Equals(item.Severity, "warning", StringComparison.OrdinalIgnoreCase))
            },
            Error = signal.Error,
            TerminalNodeId = signal.TerminalNodeId,
            VisitedNodeIds = context.VisitedNodeIds.ToArray(),
            VisitedFlowIds = context.VisitedFlowIds.ToArray(),
            SelectedCaseValues = context.SelectedCaseValues,
            MaxSteps = effectiveOptions.EffectiveMaxSteps,
            StepCount = context.Steps.Count
        };
        return Task.FromResult(result);
    }

    private NavigationSignal NavigatePath(
        MicroflowNavigationContext context,
        MicroflowExecutionPlanQuery query,
        string objectId,
        string? incomingFlowId,
        string? collectionId,
        JsonElement? loopIteration)
    {
        var currentObjectId = objectId;
        var incoming = incomingFlowId;
        while (!string.IsNullOrWhiteSpace(currentObjectId))
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (!ReserveStep(context, out var maxStepsError))
            {
                return NavigationSignal.MaxStepsExceeded(maxStepsError);
            }

            if (!query.TryGetNode(context.Plan, currentObjectId, out var node))
            {
                return NavigationSignal.Failed(Error(
                    RuntimeErrorCode.RuntimeObjectNotFound,
                    $"ExecutionPlan node not found: {currentObjectId}.",
                    objectId: currentObjectId,
                    flowId: incoming));
            }

            context.CurrentNodeId = node.ObjectId;
            context.CurrentFlowId = incoming;
            context.CurrentCollectionId = node.CollectionId;
            context.CurrentLoopObjectId = node.ParentLoopObjectId;
            context.VisitedNodeIds.Add(node.ObjectId);

            if (query.IsIgnoredNode(node))
            {
                var nextIgnored = SelectNormalFlow(context, query, node, strict: false);
                AddStep(context, node, incoming, nextIgnored.Flow?.FlowId, MicroflowNavigationStepStatus.Ignored, loopIteration, message: "Ignored runtime node.");
                if (nextIgnored.Flow is null)
                {
                    return NavigationSignal.Failed(nextIgnored.Error ?? EndNotReached(node, incoming));
                }

                VisitFlow(context, nextIgnored.Flow);
                currentObjectId = nextIgnored.Flow.DestinationObjectId;
                incoming = nextIgnored.Flow.FlowId;
                continue;
            }

            if (IsKind(node, "startEvent") || IsKind(node, "exclusiveMerge"))
            {
                var next = SelectNormalFlow(context, query, node, strict: false);
                AddStep(context, node, incoming, next.Flow?.FlowId, MicroflowNavigationStepStatus.Success, loopIteration, message: IsKind(node, "exclusiveMerge") ? "ExclusiveMerge continues immediately." : "StartEvent reached.");
                if (next.Flow is null)
                {
                    return NavigationSignal.Failed(next.Error ?? EndNotReached(node, incoming));
                }

                VisitFlow(context, next.Flow);
                currentObjectId = next.Flow.DestinationObjectId;
                incoming = next.Flow.FlowId;
                continue;
            }

            if (IsKind(node, "endEvent"))
            {
                AddStep(context, node, incoming, outgoingFlowId: null, MicroflowNavigationStepStatus.Success, loopIteration, message: "Reached EndEvent.");
                DisposeErrorScopeIfAny(context);
                return context.LoopStack.Count > 0 && string.Equals(node.CollectionId, context.LoopStack.Peek().CollectionId, StringComparison.Ordinal)
                    ? NavigationSignal.LoopIterationCompleted(node.ObjectId)
                    : NavigationSignal.Success(node.ObjectId);
            }

            if (IsKind(node, "errorEvent"))
            {
                var error = Error(RuntimeErrorCode.RuntimeErrorEventReached, node.Caption ?? "Microflow ErrorEvent reached.", node.ObjectId, flowId: incoming);
                AddStep(context, node, incoming, outgoingFlowId: null, MicroflowNavigationStepStatus.Failed, loopIteration, error, "Reached ErrorEvent.");
                DisposeErrorScopeIfAny(context);
                return NavigationSignal.Failed(error, node.ObjectId);
            }

            if (IsKind(node, "breakEvent"))
            {
                if (context.LoopStack.Count == 0)
                {
                    var error = Error(RuntimeErrorCode.RuntimeLoopControlOutOfScope, "BreakEvent is only valid inside loop context.", node.ObjectId, flowId: incoming);
                    AddStep(context, node, incoming, outgoingFlowId: null, MicroflowNavigationStepStatus.Failed, loopIteration, error);
                    return NavigationSignal.Failed(error);
                }

                context.LoopStack.Peek().BreakRequested = true;
                AddStep(context, node, incoming, outgoingFlowId: null, MicroflowNavigationStepStatus.Success, WithLoopControl(loopIteration, MicroflowLoopControlSignal.Break), message: "Break loop.", output: JsonSerializer.SerializeToElement(new { controlSignal = MicroflowLoopControlSignal.Break }, JsonOptions));
                return NavigationSignal.Break(node.ObjectId);
            }

            if (IsKind(node, "continueEvent"))
            {
                if (context.LoopStack.Count == 0)
                {
                    var error = Error(RuntimeErrorCode.RuntimeLoopControlOutOfScope, "ContinueEvent is only valid inside loop context.", node.ObjectId, flowId: incoming);
                    AddStep(context, node, incoming, outgoingFlowId: null, MicroflowNavigationStepStatus.Failed, loopIteration, error);
                    return NavigationSignal.Failed(error);
                }

                context.LoopStack.Peek().ContinueRequested = true;
                AddStep(context, node, incoming, outgoingFlowId: null, MicroflowNavigationStepStatus.Success, WithLoopControl(loopIteration, MicroflowLoopControlSignal.Continue), message: "Continue loop.", output: JsonSerializer.SerializeToElement(new { controlSignal = MicroflowLoopControlSignal.Continue }, JsonOptions));
                return NavigationSignal.Continue(node.ObjectId);
            }

            if (IsKind(node, "exclusiveSplit"))
            {
                var selected = SelectDecisionFlow(context, query, node);
                if (selected.Flow is null)
                {
                    AddStep(context, node, incoming, null, MicroflowNavigationStepStatus.Failed, loopIteration, selected.Error, selected.Error?.Message, selected.SelectedCaseValue, selected.Output);
                    return NavigationSignal.Failed(selected.Error ?? InvalidCase(node, "decision"));
                }

                AddSelectedCase(context, node.ObjectId, selected);
                AddStep(context, node, incoming, selected.Flow.FlowId, MicroflowNavigationStepStatus.Success, loopIteration, selectedCaseValue: selected.SelectedCaseValue, message: $"Decision selected {selected.SelectedCaseText}.", output: selected.Output);
                VisitFlow(context, selected.Flow);
                currentObjectId = selected.Flow.DestinationObjectId;
                incoming = selected.Flow.FlowId;
                continue;
            }

            if (IsKind(node, "inheritanceSplit"))
            {
                var selected = SelectObjectTypeFlow(context, query, node);
                if (selected.Flow is null)
                {
                    AddStep(context, node, incoming, null, MicroflowNavigationStepStatus.Failed, loopIteration, selected.Error, selected.Error?.Message, selected.SelectedCaseValue);
                    return NavigationSignal.Failed(selected.Error ?? InvalidCase(node, "objectType"));
                }

                AddSelectedCase(context, node.ObjectId, selected);
                AddStep(context, node, incoming, selected.Flow.FlowId, MicroflowNavigationStepStatus.Success, loopIteration, selectedCaseValue: selected.SelectedCaseValue, message: $"ObjectType decision selected {selected.SelectedCaseText}.");
                VisitFlow(context, selected.Flow);
                currentObjectId = selected.Flow.DestinationObjectId;
                incoming = selected.Flow.FlowId;
                continue;
            }

            if (IsKind(node, "loopedActivity"))
            {
                var loopSignal = NavigateLoop(context, query, node, incoming, loopIteration);
                if (loopSignal.Kind is NavigationSignalKind.Failed or NavigationSignalKind.MaxStepsExceeded or NavigationSignalKind.Cancelled)
                {
                    return loopSignal;
                }

                var next = SelectNormalFlow(context, query, node, strict: false);
                if (next.Flow is null)
                {
                    return NavigationSignal.Failed(next.Error ?? EndNotReached(node, incoming));
                }

                VisitFlow(context, next.Flow);
                currentObjectId = next.Flow.DestinationObjectId;
                incoming = next.Flow.FlowId;
                continue;
            }

            if (IsKind(node, "actionActivity"))
            {
                var actionSignal = NavigateAction(context, query, node, incoming, loopIteration);
                if (actionSignal.Kind is NavigationSignalKind.Failed or NavigationSignalKind.MaxStepsExceeded or NavigationSignalKind.Cancelled)
                {
                    return actionSignal;
                }

                if (actionSignal.NextFlow is null)
                {
                    return NavigationSignal.Failed(EndNotReached(node, incoming));
                }

                VisitFlow(context, actionSignal.NextFlow);
                currentObjectId = actionSignal.NextFlow.DestinationObjectId;
                incoming = actionSignal.NextFlow.FlowId;
                continue;
            }

            var unsupported = Error(RuntimeErrorCode.RuntimeUnsupportedAction, $"Runtime node kind is unsupported: {node.Kind}.", node.ObjectId, node.ActionId, incoming);
            AddStep(context, node, incoming, null, MicroflowNavigationStepStatus.Failed, loopIteration, unsupported);
            return NavigationSignal.Failed(unsupported);
        }

        return NavigationSignal.Failed(Error(RuntimeErrorCode.RuntimeEndNotReached, "Navigation path ended before reaching EndEvent."));
    }

    private NavigationSignal NavigateLoop(
        MicroflowNavigationContext context,
        MicroflowExecutionPlanQuery query,
        MicroflowExecutionNode loopNode,
        string? incomingFlowId,
        JsonElement? outerLoopIteration)
    {
        var loop = query.GetLoopCollection(context.Plan, loopNode.ObjectId);
        if (loop is null)
        {
            var error = Error(RuntimeErrorCode.RuntimeFlowNotFound, "Loop collection is missing.", loopNode.ObjectId, loopNode.ActionId, incomingFlowId);
            AddStep(context, loopNode, incomingFlowId, null, MicroflowNavigationStepStatus.Failed, outerLoopIteration, error);
            return NavigationSignal.Failed(error);
        }

        var entryNodeId = query.FindLoopEntryNodeId(context.Plan, loop);
        if (string.IsNullOrWhiteSpace(entryNodeId))
        {
            var error = Error(RuntimeErrorCode.RuntimeLoopBodyNotFound, "Loop internal entry node is missing.", loopNode.ObjectId, loopNode.ActionId, incomingFlowId);
            return NavigationSignal.Failed(error);
        }

        var frame = new MicroflowNavigationLoopFrame
        {
            LoopObjectId = loop.LoopObjectId,
            CollectionId = loop.CollectionId,
            MaxIterations = context.Options.LoopIterations ?? 1000
        };
        context.LoopStack.Push(frame);
        MicroflowLoopExecutionResult loopResult;
        try
        {
            var actionContext = new MicroflowActionExecutionContext
            {
                RuntimeExecutionContext = context.RuntimeContext,
                ExecutionPlan = context.Plan,
                ExecutionNode = loopNode,
                ActionConfig = loopNode.ConfigJson ?? default,
                ActionKind = loopNode.ActionKind ?? "loopedActivity",
                ObjectId = loopNode.ObjectId,
                ActionId = loopNode.ActionId,
                CollectionId = loopNode.CollectionId,
                VariableStore = context.RuntimeContext.VariableStore,
                ExpressionEvaluator = _expressionEvaluator,
                TransactionManager = context.RuntimeContext.TransactionManager,
                ConnectorRegistry = _connectorRegistry,
                RuntimeSecurityContext = context.RuntimeContext.RuntimeSecurityContext,
                Options = new MicroflowActionExecutionOptions
                {
                    Mode = context.Options.Mode,
                    StopOnUnsupported = context.Options.EffectiveStopOnUnsupported
                },
                LoopExecutionOptions = new MicroflowLoopExecutionOptions
                {
                    MaxIterations = context.Options.LoopIterations is > 0 ? context.Options.LoopIterations.Value : 1000,
                    LoopIterationsOverride = context.Options.LoopIterations,
                    StopOnActionError = context.Options.StopOnFirstError
                },
                LoopBodyExecutor = (iteration, ct) =>
                {
                    frame.CurrentIndex = iteration.Index;
                    frame.BreakRequested = false;
                    frame.ContinueRequested = false;
                    var signal = NavigatePath(context, query, entryNodeId!, null, loop.CollectionId, iteration.LoopIterationJson);
                    frame.BreakRequested = signal.Kind == NavigationSignalKind.Break;
                    frame.ContinueRequested = signal.Kind == NavigationSignalKind.Continue;
                    return Task.FromResult(ToLoopBodyExecutionResult(signal));
                }
            };
            loopResult = _loopExecutor.ExecuteLoopAsync(actionContext, loopNode, context.CancellationToken).GetAwaiter().GetResult();
        }
        finally
        {
            context.LoopStack.Pop();
        }

        var status = loopResult.Status is MicroflowLoopExecutionStatus.Failed or MicroflowLoopExecutionStatus.MaxIterationsExceeded or MicroflowLoopExecutionStatus.Cancelled
            ? MicroflowNavigationStepStatus.Failed
            : MicroflowNavigationStepStatus.Success;
        var message = loopResult.Status switch
        {
            MicroflowLoopExecutionStatus.Break => "Break loop.",
            MicroflowLoopExecutionStatus.Continue => "Continue loop.",
            MicroflowLoopExecutionStatus.MaxIterationsExceeded => "Loop maxIterations exceeded.",
            MicroflowLoopExecutionStatus.Cancelled => "Loop cancelled.",
            MicroflowLoopExecutionStatus.Failed => "Loop failed.",
            _ => $"Loop executed iterations={loopResult.IterationCount}."
        };
        AddStep(
            context,
            loopNode,
            incomingFlowId,
            query.GetDefaultNormalOutgoingFlow(context.Plan, loopNode.ObjectId, loopNode.CollectionId)?.FlowId,
            status,
            outerLoopIteration,
            loopResult.Error is null ? null : ToNavigationError(loopResult.Error, incomingFlowId),
            message,
            output: loopResult.OutputPreview);
        if (loopResult.Status is MicroflowLoopExecutionStatus.Failed or MicroflowLoopExecutionStatus.MaxIterationsExceeded)
        {
            return NavigationSignal.Failed(loopResult.Error is null ? Error(RuntimeErrorCode.RuntimeLoopDeadEnd, "Loop failed.", loopNode.ObjectId, loopNode.ActionId, incomingFlowId) : ToNavigationError(loopResult.Error, incomingFlowId));
        }
        if (loopResult.Status == MicroflowLoopExecutionStatus.Cancelled)
        {
            return NavigationSignal.Cancelled(loopResult.Error is null ? CancelledError() : ToNavigationError(loopResult.Error, incomingFlowId));
        }
        return NavigationSignal.LoopIterationCompleted(loopNode.ObjectId);
    }

    private static MicroflowLoopBodyExecutionResult ToLoopBodyExecutionResult(NavigationSignal signal)
        => signal.Kind switch
        {
            NavigationSignalKind.Break => new MicroflowLoopBodyExecutionResult { Status = MicroflowLoopBodyExecutionStatus.Break },
            NavigationSignalKind.Continue => new MicroflowLoopBodyExecutionResult { Status = MicroflowLoopBodyExecutionStatus.Continue },
            NavigationSignalKind.Cancelled => new MicroflowLoopBodyExecutionResult { Status = MicroflowLoopBodyExecutionStatus.Cancelled, Error = signal.Error is null ? null : ToRuntimeError(signal.Error) },
            NavigationSignalKind.MaxStepsExceeded => new MicroflowLoopBodyExecutionResult { Status = MicroflowLoopBodyExecutionStatus.MaxStepsExceeded, Error = signal.Error is null ? null : ToRuntimeError(signal.Error) },
            NavigationSignalKind.Failed => new MicroflowLoopBodyExecutionResult { Status = MicroflowLoopBodyExecutionStatus.Failed, Error = signal.Error is null ? null : ToRuntimeError(signal.Error) },
            NavigationSignalKind.LoopIterationCompleted => new MicroflowLoopBodyExecutionResult { Status = MicroflowLoopBodyExecutionStatus.IterationCompleted },
            _ => new MicroflowLoopBodyExecutionResult { Status = MicroflowLoopBodyExecutionStatus.Success }
        };

    private static MicroflowRuntimeErrorDto ToRuntimeError(MicroflowNavigationError error)
        => new()
        {
            Code = error.Code,
            Message = error.Message,
            ObjectId = error.ObjectId,
            ActionId = error.ActionId,
            FlowId = error.FlowId,
            Details = error.Details,
            Cause = error.Cause
        };

    private static MicroflowNavigationError ToNavigationError(MicroflowRuntimeErrorDto error, string? fallbackFlowId)
        => new()
        {
            Code = error.Code,
            Message = error.Message,
            ObjectId = error.ObjectId,
            ActionId = error.ActionId,
            FlowId = error.FlowId ?? fallbackFlowId,
            Details = error.Details,
            Cause = error.Cause
        };

    private NavigationSignal NavigateAction(
        MicroflowNavigationContext context,
        MicroflowExecutionPlanQuery query,
        MicroflowExecutionNode node,
        string? incomingFlowId,
        JsonElement? loopIteration)
    {
        var next = SelectNormalFlow(context, query, node, strict: false).Flow;
        var executor = _actionExecutorRegistry.GetOrFallback(node.ActionKind);
        if (context.Options.SimulateActionFailureObjectIds.Contains(node.ObjectId, StringComparer.Ordinal))
        {
            var failure = Error(RuntimeErrorCode.RuntimeUnknownError, "Action failure simulated by FlowNavigator options.", node.ObjectId, node.ActionId, incomingFlowId);
            return HandleActionFailure(context, query, node, incomingFlowId, loopIteration, failure);
        }

        if (string.Equals(node.ActionKind, "restCall", StringComparison.OrdinalIgnoreCase) && context.Options.SimulateRestError)
        {
            var restError = Error(RuntimeErrorCode.RuntimeRestCallFailed, "REST call failed by FlowNavigator simulation.", node.ObjectId, node.ActionId, incomingFlowId, details: "No external HTTP request was sent.");
            return HandleActionFailure(context, query, node, incomingFlowId, loopIteration, restError);
        }

        if (string.Equals(node.SupportLevel, MicroflowRuntimeSupportLevel.Supported, StringComparison.OrdinalIgnoreCase))
        {
            if (executor.Category == MicroflowActionRuntimeCategory.ServerExecutable)
            {
                WritePlaceholderActionOutput(context, node);
            }

            AddStep(
                context,
                node,
                incomingFlowId,
                next?.FlowId,
                MicroflowNavigationStepStatus.Success,
                loopIteration,
                message: executor.Category == MicroflowActionRuntimeCategory.RuntimeCommand
                    ? "Action produced a RuntimeCommand for client handling."
                    : "Action resolved through ActionExecutorRegistry.",
                output: BuildNavigatorExecutorOutput(node, executor));
            return NavigationSignal.ContinueWith(next);
        }

        if (string.Equals(node.SupportLevel, MicroflowRuntimeSupportLevel.ModeledOnly, StringComparison.OrdinalIgnoreCase)
            && !context.Options.EffectiveStopOnUnsupported)
        {
            AddDiagnostic(context, "RUNTIME_MODELED_ONLY_SKIPPED", "warning", "Modeled-only action skipped by dry-run FlowNavigator.", node.ObjectId, actionId: node.ActionId, collectionId: node.CollectionId);
            AddStep(context, node, incomingFlowId, next?.FlowId, MicroflowNavigationStepStatus.Skipped, loopIteration, message: "Modeled-only action skipped by FlowNavigator.");
            return NavigationSignal.ContinueWith(next);
        }

        var code = string.Equals(node.SupportLevel, MicroflowRuntimeSupportLevel.RequiresConnector, StringComparison.OrdinalIgnoreCase)
            ? RuntimeErrorCode.RuntimeConnectorRequired
            : RuntimeErrorCode.RuntimeUnsupportedAction;
        var message = string.Equals(node.SupportLevel, MicroflowRuntimeSupportLevel.NanoflowOnly, StringComparison.OrdinalIgnoreCase)
            ? "Nanoflow-only action cannot run in Microflow runtime."
            : executor.Category == MicroflowActionRuntimeCategory.ConnectorBacked
                ? $"Connector capability required for action: {node.ActionKind ?? "missing"}."
                : $"Action is not executable by FlowNavigator: {node.ActionKind ?? "missing"}.";
        var error = Error(code, message, node.ObjectId, node.ActionId, incomingFlowId);
        AddStep(context, node, incomingFlowId, null, MicroflowNavigationStepStatus.Failed, loopIteration, error, output: BuildNavigatorExecutorOutput(node, executor));
        return NavigationSignal.Failed(error);
    }

    private static JsonElement BuildNavigatorExecutorOutput(MicroflowExecutionNode node, IMicroflowActionExecutor executor)
    {
        var command = executor.Category == MicroflowActionRuntimeCategory.RuntimeCommand
            ? new MicroflowRuntimeCommand
            {
                CommandKind = node.ActionKind ?? executor.ActionKind,
                SourceObjectId = node.ObjectId,
                SourceActionId = node.ActionId,
                PayloadJson = node.ConfigJson?.GetRawText(),
                Message = "Client must handle this RuntimeCommand."
            }
            : null;
        var connectorRequest = executor is ConfiguredMicroflowActionExecutor configured
            && configured.Descriptor.ConnectorCapability is not null
                ? new MicroflowConnectorExecutionRequest
                {
                    Capability = configured.Descriptor.ConnectorCapability,
                    ActionKind = node.ActionKind ?? executor.ActionKind,
                    ObjectId = node.ObjectId,
                    ActionId = node.ActionId,
                    PayloadJson = node.ConfigJson?.GetRawText()
                }
                : null;
        return JsonSerializer.SerializeToElement(new
        {
            actionKind = node.ActionKind,
            executorCategory = executor.Category,
            supportLevel = executor.SupportLevel,
            outputPreview = executor.Category == MicroflowActionRuntimeCategory.RuntimeCommand
                ? "pending client command"
                : executor.Category,
            runtimeCommands = command is null ? Array.Empty<MicroflowRuntimeCommand>() : new[] { command },
            connectorRequests = connectorRequest is null ? Array.Empty<MicroflowConnectorExecutionRequest>() : new[] { connectorRequest },
            diagnostics = Array.Empty<MicroflowActionExecutionDiagnostic>(),
            durationMs = 0
        }, JsonOptions);
    }

    private NavigationSignal HandleActionFailure(
        MicroflowNavigationContext context,
        MicroflowExecutionPlanQuery query,
        MicroflowExecutionNode node,
        string? incomingFlowId,
        JsonElement? loopIteration,
        MicroflowNavigationError error)
    {
        if (context.Options.StopOnFirstError)
        {
            AddStep(context, node, incomingFlowId, null, MicroflowNavigationStepStatus.Failed, loopIteration, error);
            return NavigationSignal.Failed(error);
        }

        var handler = SelectErrorHandlerFlow(context, query, node, error);
        AddStep(context, node, incomingFlowId, handler?.FlowId, MicroflowNavigationStepStatus.Failed, loopIteration, error, "Action failure entered FlowNavigator error handling skeleton.");
        if (handler is null)
        {
            return NavigationSignal.Failed(error);
        }

        context.ErrorStack.Push(new MicroflowNavigationErrorFrame
        {
            SourceObjectId = node.ObjectId,
            SourceActionId = node.ActionId,
            Error = error,
            ErrorHandlerFlowId = handler.FlowId,
            LatestHttpResponse = string.Equals(error.Code, RuntimeErrorCode.RuntimeRestCallFailed, StringComparison.Ordinal)
                ? JsonSerializer.SerializeToElement(new { statusCode = 500, body = "flow-navigator-rest-error" }, JsonOptions)
                : null
        });
        context.ErrorScopeStack.Push(context.RuntimeContext.PushErrorHandlerScope(
            new MicroflowRuntimeErrorDto
            {
                Code = error.Code,
                Message = error.Message,
                ObjectId = error.ObjectId,
                ActionId = error.ActionId,
                FlowId = error.FlowId,
                Details = error.Details,
                Cause = error.Cause
            },
            handler.FlowId,
            string.Equals(error.Code, RuntimeErrorCode.RuntimeRestCallFailed, StringComparison.Ordinal)
                ? JsonSerializer.SerializeToElement(new { statusCode = 500, body = "flow-navigator-rest-error" }, JsonOptions)
                : null));
        VisitFlow(context, handler);
        AddDiagnostic(context, "RUNTIME_ERROR_HANDLER_ENTERED", "warning", "FlowNavigator entered error handler path; transaction semantics are not executed.", node.ObjectId, handler.FlowId, node.ActionId, node.CollectionId);
        return NavigationSignal.ContinueWith(handler);
    }

    private static void WritePlaceholderActionOutput(MicroflowNavigationContext context, MicroflowExecutionNode node)
    {
        if (node.ConfigJson is null || node.ConfigJson.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return;
        }

        var config = node.ConfigJson.Value;
        var outputName = ReadString(config, "outputVariableName")
            ?? ReadString(config, "resultVariableName")
            ?? ReadStringByPath(config, "returnValue", "outputVariableName")
            ?? ReadStringByPath(config, "response", "handling", "outputVariableName")
            ?? ReadStringByPath(config, "action", "outputVariableName")
            ?? ReadStringByPath(config, "action", "resultVariableName")
            ?? ReadStringByPath(config, "action", "returnValue", "outputVariableName")
            ?? ReadStringByPath(config, "action", "response", "handling", "outputVariableName");
        if (string.IsNullOrWhiteSpace(outputName))
        {
            return;
        }

        var type = node.ActionKind switch
        {
            "retrieve" => JsonSerializer.Serialize(new { kind = "list" }, JsonOptions),
            "createObject" => JsonSerializer.Serialize(new { kind = "object" }, JsonOptions),
            "restCall" => JsonSerializer.Serialize(new { kind = "httpResponse" }, JsonOptions),
            _ => JsonSerializer.Serialize(new { kind = "unknown" }, JsonOptions)
        };
        var raw = node.ActionKind switch
        {
            "retrieve" => JsonSerializer.Serialize(new { items = Array.Empty<object>(), source = "flow-navigator-placeholder" }, JsonOptions),
            "createObject" => JsonSerializer.Serialize(new { id = $"{node.ObjectId}-placeholder", source = "flow-navigator-placeholder" }, JsonOptions),
            "restCall" => JsonSerializer.Serialize(new { statusCode = 200, body = "flow-navigator-placeholder" }, JsonOptions),
            _ => JsonSerializer.Serialize(new { source = "flow-navigator-placeholder" }, JsonOptions)
        };
        var sourceKind = node.ActionKind switch
        {
            "createVariable" => MicroflowVariableSourceKind.LocalVariable,
            "callMicroflow" => MicroflowVariableSourceKind.MicroflowReturn,
            "restCall" => MicroflowVariableSourceKind.RestResponse,
            _ => MicroflowVariableSourceKind.ActionOutput
        };

        var value = new MicroflowRuntimeVariableValue
        {
            Name = outputName!,
            DataTypeJson = type,
            Kind = MicroflowVariableStore.InferKind(type, raw),
            RawValueJson = raw,
            ValuePreview = MicroflowVariableStore.Preview(raw),
            SourceKind = sourceKind,
            SourceObjectId = node.ObjectId,
            SourceActionId = node.ActionId,
            CollectionId = node.CollectionId,
            ScopeKind = node.ParentLoopObjectId is null ? MicroflowVariableScopeKind.Action : MicroflowVariableScopeKind.Loop,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        try
        {
            if (context.RuntimeContext.VariableStore.Exists(outputName!))
            {
                context.RuntimeContext.VariableStore.Set(outputName!, value);
            }
            else
            {
                context.RuntimeContext.VariableStore.Define(new MicroflowVariableDefinition
                {
                    Name = outputName!,
                    DataTypeJson = type,
                    Value = value,
                    SourceKind = sourceKind,
                    SourceObjectId = node.ObjectId,
                    SourceActionId = node.ActionId,
                    CollectionId = node.CollectionId,
                    ScopeKind = value.ScopeKind
                });
            }
        }
        catch (MicroflowVariableStoreException ex)
        {
            AddDiagnostic(context, ex.Diagnostic.Code, ex.Diagnostic.Severity, ex.Diagnostic.Message, node.ObjectId, actionId: node.ActionId, collectionId: node.CollectionId);
        }
    }

    private MicroflowFlowSelectionResult SelectNormalFlow(
        MicroflowNavigationContext context,
        MicroflowExecutionPlanQuery query,
        MicroflowExecutionNode node,
        bool strict)
    {
        var flows = query.GetNormalOutgoingFlows(context.Plan, node.ObjectId, node.CollectionId);
        if (flows.Count == 0)
        {
            return new MicroflowFlowSelectionResult { Error = EndNotReached(node, context.CurrentFlowId) };
        }
        if (flows.Count > 1)
        {
            AddDiagnostic(context, "RUNTIME_MULTIPLE_NORMAL_OUTGOING", strict ? "error" : "warning", "Multiple normal outgoing flows found; FlowNavigator selected the first deterministic flow.", node.ObjectId, collectionId: node.CollectionId);
            if (strict)
            {
                return new MicroflowFlowSelectionResult { Error = Error(RuntimeErrorCode.RuntimeFlowNotFound, "Multiple normal outgoing flows are invalid for this node.", node.ObjectId) };
            }
        }

        return new MicroflowFlowSelectionResult { Flow = flows[0] };
    }

    private MicroflowFlowSelectionResult SelectDecisionFlow(
        MicroflowNavigationContext context,
        MicroflowExecutionPlanQuery query,
        MicroflowExecutionNode node)
    {
        MicroflowExpressionEvaluationResult? expressionResult = null;
        var target = context.Options.EnumerationCaseValue;
        if (string.IsNullOrWhiteSpace(target) && context.Options.DecisionBooleanResult is not null)
        {
            target = context.Options.DecisionBooleanResult.Value ? "true" : "false";
        }

        if (string.IsNullOrWhiteSpace(target) && !context.Options.DisableExpressionEvaluation)
        {
            var expression = ReadDecisionExpression(node);
            if (!string.IsNullOrWhiteSpace(expression))
            {
                expressionResult = _expressionEvaluator.Evaluate(
                    expression!,
                    new MicroflowExpressionEvaluationContext
                    {
                        RuntimeExecutionContext = context.RuntimeContext,
                        VariableStore = context.RuntimeContext.VariableStore,
                        CurrentObjectId = node.ObjectId,
                        CurrentActionId = node.ActionId,
                        CurrentCollectionId = node.CollectionId,
                        CurrentFlowId = context.CurrentFlowId,
                        ExpectedType = MicroflowExpressionType.Simple(MicroflowExpressionTypeKind.Boolean),
                        Mode = context.Options.Mode,
                        Options = new MicroflowExpressionEvaluationOptions
                        {
                            AllowUnknownVariables = false,
                            AllowUnsupportedFunctions = false,
                            StrictTypeCheck = true
                        }
                    });
                if (!expressionResult.Success)
                {
                    var error = Error(
                        RuntimeErrorCode.RuntimeExpressionError,
                        expressionResult.Error?.Message ?? "Decision expression evaluation failed.",
                        node.ObjectId,
                        node.ActionId,
                        context.CurrentFlowId,
                        JsonSerializer.Serialize(expressionResult.Diagnostics, JsonOptions));
                    return new MicroflowFlowSelectionResult
                    {
                        Error = error,
                        Output = JsonSerializer.SerializeToElement(new { expressionResult }, JsonOptions)
                    };
                }

                target = expressionResult.Value?.BoolValue == true ? "true" : "false";
            }
        }

        if (string.IsNullOrWhiteSpace(target))
        {
            target = "true";
            AddDiagnostic(context, "RUNTIME_DECISION_DEFAULTED", "warning", "Decision expression not evaluated, using default true.", node.ObjectId, collectionId: node.CollectionId);
        }

        var targetValue = target!;
        var flows = query.GetDecisionOutgoingFlows(context.Plan, node.ObjectId, node.CollectionId);
        var selected = MatchFlowByCase(flows, targetValue);
        selected = selected with
        {
            Output = expressionResult is null
                ? null
                : JsonSerializer.SerializeToElement(new { expressionResult }, JsonOptions)
        };
        if (selected.Flow is not null)
        {
            return selected;
        }

        if (string.IsNullOrWhiteSpace(context.Options.EnumerationCaseValue))
        {
            var fallback = SelectFallbackCase(flows);
            if (fallback.Flow is not null)
            {
                return fallback with { Output = selected.Output };
            }
        }

        return selected with { Error = InvalidCase(node, targetValue) };
    }

    private MicroflowFlowSelectionResult SelectObjectTypeFlow(
        MicroflowNavigationContext context,
        MicroflowExecutionPlanQuery query,
        MicroflowExecutionNode node)
    {
        var flows = query.GetObjectTypeOutgoingFlows(context.Plan, node.ObjectId, node.CollectionId);
        if (!string.IsNullOrWhiteSpace(context.Options.ObjectTypeCase))
        {
            var selected = MatchFlowByCase(flows, context.Options.ObjectTypeCase!);
            return selected.Flow is not null
                ? selected
                : selected with { Error = InvalidCase(node, context.Options.ObjectTypeCase!) };
        }

        var fallback = SelectFallbackCase(flows);
        if (fallback.Flow is not null)
        {
            return fallback;
        }

        var first = FirstCase(flows);
        return first.Flow is not null
            ? first
            : first with { Error = InvalidCase(node, "objectType") };
    }

    private MicroflowExecutionFlow? SelectErrorHandlerFlow(
        MicroflowNavigationContext context,
        MicroflowExecutionPlanQuery query,
        MicroflowExecutionNode node,
        MicroflowNavigationError error)
    {
        var flows = query.GetErrorHandlerFlows(context.Plan, node.ObjectId, node.CollectionId);
        if (flows.Count == 0)
        {
            return null;
        }

        if (flows.Count > 1)
        {
            AddDiagnostic(context, "RUNTIME_ERROR_HANDLER_DUPLICATED", "error", "Multiple error handler flows found; FlowNavigator selected the first deterministic flow.", node.ObjectId, actionId: node.ActionId, collectionId: node.CollectionId);
            if (string.Equals(context.Options.Mode, MicroflowNavigationMode.TestRun, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }

        return flows[0];
    }

    private static MicroflowFlowSelectionResult MatchFlowByCase(IReadOnlyList<MicroflowExecutionFlow> flows, string target)
    {
        foreach (var flow in flows)
        {
            foreach (var caseValue in flow.CaseValues)
            {
                var token = CaseToken(caseValue);
                if (string.Equals(token, target, StringComparison.OrdinalIgnoreCase))
                {
                    return new MicroflowFlowSelectionResult
                    {
                        Flow = flow,
                        SelectedCaseValue = caseValue.Clone(),
                        SelectedCaseText = token
                    };
                }
            }
        }

        return new MicroflowFlowSelectionResult { SelectedCaseText = target };
    }

    private static MicroflowFlowSelectionResult SelectFallbackCase(IReadOnlyList<MicroflowExecutionFlow> flows)
    {
        foreach (var expected in new[] { "fallback", "empty", "noCase" })
        {
            var selected = MatchFlowByCase(flows, expected);
            if (selected.Flow is not null)
            {
                return selected;
            }
        }

        return new MicroflowFlowSelectionResult();
    }

    private static MicroflowFlowSelectionResult FirstCase(IReadOnlyList<MicroflowExecutionFlow> flows)
    {
        foreach (var flow in flows)
        {
            var caseValue = flow.CaseValues.FirstOrDefault();
            if (caseValue.ValueKind != JsonValueKind.Undefined)
            {
                return new MicroflowFlowSelectionResult
                {
                    Flow = flow,
                    SelectedCaseValue = caseValue.Clone(),
                    SelectedCaseText = CaseToken(caseValue)
                };
            }
        }

        return new MicroflowFlowSelectionResult();
    }

    private bool ReserveStep(MicroflowNavigationContext context, out MicroflowNavigationError error)
    {
        context.StepIndex++;
        if (context.StepIndex <= context.Options.EffectiveMaxSteps)
        {
            error = new MicroflowNavigationError();
            return true;
        }

        error = Error(RuntimeErrorCode.RuntimeMaxStepsExceeded, $"FlowNavigator exceeded maxSteps={context.Options.EffectiveMaxSteps}.", context.CurrentNodeId, flowId: context.CurrentFlowId);
        return false;
    }

    private void AddStep(
        MicroflowNavigationContext context,
        MicroflowExecutionNode node,
        string? incomingFlowId,
        string? outgoingFlowId,
        string status,
        JsonElement? loopIteration,
        MicroflowNavigationError? error = null,
        string? message = null,
        JsonElement? selectedCaseValue = null,
        JsonElement? output = null)
    {
        var startedAt = _clock.UtcNow;
        var endedAt = _clock.UtcNow;
        var sequence = context.Steps.Count + 1;
        context.Steps.Add(new MicroflowNavigationStep
        {
            Sequence = sequence,
            ObjectId = node.ObjectId,
            ActionId = node.ActionId,
            CollectionId = node.CollectionId,
            IncomingFlowId = incomingFlowId,
            OutgoingFlowId = outgoingFlowId,
            NodeKind = node.Kind,
            ActionKind = node.ActionKind,
            Status = status,
            SelectedCaseValue = selectedCaseValue,
            LoopIteration = loopIteration,
            Error = error,
            Message = message,
            Output = output,
            VariablesSnapshot = context.RuntimeContext.CreateSnapshot(node.ObjectId, node.ActionId, node.CollectionId, sequence).ToRuntimeVariableDtos(),
            StartedAt = startedAt,
            EndedAt = endedAt,
            DurationMs = DurationMs(startedAt, endedAt)
        });
    }

    private static MicroflowNavigationTraceFrame ToTraceFrame(MicroflowNavigationStep step)
        => new()
        {
            Sequence = step.Sequence,
            ObjectId = step.ObjectId,
            ActionId = step.ActionId,
            CollectionId = step.CollectionId,
            IncomingFlowId = step.IncomingFlowId,
            OutgoingFlowId = step.OutgoingFlowId,
            SelectedCaseValue = step.SelectedCaseValue,
            LoopIteration = step.LoopIteration,
            Status = step.Status,
            StartedAt = step.StartedAt,
            EndedAt = step.EndedAt,
            DurationMs = step.DurationMs,
            Error = step.Error,
            Message = step.Message,
            VariablesSnapshot = step.VariablesSnapshot,
            Output = step.Output
        };

    private static void VisitFlow(MicroflowNavigationContext context, MicroflowExecutionFlow flow)
    {
        context.CurrentFlowId = flow.FlowId;
        context.VisitedFlowIds.Add(flow.FlowId);
    }

    private static JsonElement? WithLoopControl(JsonElement? loopIteration, string controlSignal)
    {
        if (!loopIteration.HasValue || loopIteration.Value.ValueKind != JsonValueKind.Object)
        {
            return loopIteration;
        }

        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["controlSignal"] = controlSignal
        };
        foreach (var property in loopIteration.Value.EnumerateObject())
        {
            if (!string.Equals(property.Name, "controlSignal", StringComparison.Ordinal))
            {
                values[property.Name] = property.Value.Clone();
            }
        }

        return JsonSerializer.SerializeToElement(values, JsonOptions);
    }

    private static void DisposeErrorScopeIfAny(MicroflowNavigationContext context)
    {
        if (context.ErrorScopeStack.Count > 0)
        {
            context.ErrorScopeStack.Pop().Dispose();
        }
        if (context.ErrorStack.Count > 0)
        {
            context.ErrorStack.Pop();
        }
    }

    private static void AddSelectedCase(MicroflowNavigationContext context, string objectId, MicroflowFlowSelectionResult selected)
    {
        if (selected.SelectedCaseValue.HasValue)
        {
            context.SelectedCaseValues[objectId] = selected.SelectedCaseValue.Value.Clone();
        }
    }

    private static void AddDiagnostic(MicroflowNavigationContext context, string code, string severity, string message, string? objectId = null, string? flowId = null, string? actionId = null, string? collectionId = null)
        => context.Diagnostics.Add(new MicroflowExecutionDiagnosticDto
        {
            Code = code,
            Severity = severity,
            Message = message,
            ObjectId = objectId,
            FlowId = flowId,
            ActionId = actionId,
            CollectionId = collectionId
        });

    private static MicroflowExecutionDiagnosticDto ToExecutionDiagnostic(MicroflowVariableStoreDiagnostic diagnostic)
        => new()
        {
            Code = diagnostic.Code,
            Severity = diagnostic.Severity,
            Message = diagnostic.Message,
            ObjectId = diagnostic.ObjectId,
            ActionId = diagnostic.ActionId,
            CollectionId = diagnostic.CollectionId,
            FieldPath = diagnostic.VariableName
        };

    private static string? CaseToken(JsonElement caseValue)
    {
        if (caseValue.ValueKind == JsonValueKind.Object)
        {
            if (caseValue.TryGetProperty("value", out var value))
            {
                return ScalarToken(value);
            }
            if (caseValue.TryGetProperty("persistedValue", out var persistedValue))
            {
                return ScalarToken(persistedValue);
            }
            if (caseValue.TryGetProperty("entityQualifiedName", out var entityQualifiedName))
            {
                return ScalarToken(entityQualifiedName);
            }
            if (caseValue.TryGetProperty("kind", out var kind))
            {
                return ScalarToken(kind);
            }
        }

        return ScalarToken(caseValue);
    }

    private static string? ScalarToken(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => value.GetRawText(),
            _ => value.GetRawText().Trim('"')
        };

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

    private static string? ReadDecisionExpression(MicroflowExecutionNode node)
    {
        if (node.ConfigJson is null || node.ConfigJson.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var config = node.ConfigJson.Value;
        return ReadExpressionTextByPath(config, "raw", "splitCondition", "expression")
            ?? ReadExpressionTextByPath(config, "raw", "config", "expression")
            ?? ReadExpressionTextByPath(config, "raw", "expression")
            ?? ReadExpressionTextByPath(config, "action", "expression");
    }

    private static string? ReadExpressionTextByPath(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var segment in path[..^1])
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return ReadExpressionText(current, path[^1]);
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

        return value.ValueKind == JsonValueKind.Object
            ? ReadString(value, "raw") ?? ReadString(value, "text") ?? ReadString(value, "expression")
            : null;
    }

    private static MicroflowNavigationError InvalidCase(MicroflowExecutionNode node, string selectedValue)
        => Error(RuntimeErrorCode.RuntimeInvalidCase, $"No matching navigation case found: {selectedValue}.", node.ObjectId, node.ActionId);

    private static MicroflowNavigationError EndNotReached(MicroflowExecutionNode node, string? flowId)
        => Error(RuntimeErrorCode.RuntimeEndNotReached, $"Node {node.ObjectId} has no navigable outgoing flow.", node.ObjectId, node.ActionId, flowId);

    private static MicroflowNavigationError CancelledError()
        => Error(RuntimeErrorCode.RuntimeCancelled, "FlowNavigator was cancelled.");

    private static MicroflowNavigationError Error(string code, string message, string? objectId = null, string? actionId = null, string? flowId = null, string? details = null)
        => new()
        {
            Code = code,
            Message = message,
            ObjectId = objectId,
            ActionId = actionId,
            FlowId = flowId,
            Details = details
        };

    private static bool IsKind(MicroflowExecutionNode node, string kind)
        => string.Equals(node.Kind, kind, StringComparison.OrdinalIgnoreCase);

    private static int DurationMs(DateTimeOffset startedAt, DateTimeOffset endedAt)
        => Math.Max(0, (int)(endedAt - startedAt).TotalMilliseconds);

    private enum NavigationSignalKind
    {
        Success,
        Failed,
        Cancelled,
        MaxStepsExceeded,
        Break,
        Continue,
        LoopIterationCompleted,
        ContinueWith
    }

    private sealed record NavigationSignal(NavigationSignalKind Kind, MicroflowNavigationError? Error = null, string? TerminalNodeId = null, MicroflowExecutionFlow? NextFlow = null)
    {
        public static NavigationSignal Success(string terminalNodeId) => new(NavigationSignalKind.Success, TerminalNodeId: terminalNodeId);

        public static NavigationSignal Failed(MicroflowNavigationError error, string? terminalNodeId = null) => new(NavigationSignalKind.Failed, error, terminalNodeId);

        public static NavigationSignal Cancelled(MicroflowNavigationError error) => new(NavigationSignalKind.Cancelled, error);

        public static NavigationSignal MaxStepsExceeded(MicroflowNavigationError error) => new(NavigationSignalKind.MaxStepsExceeded, error);

        public static NavigationSignal Break(string terminalNodeId) => new(NavigationSignalKind.Break, TerminalNodeId: terminalNodeId);

        public static NavigationSignal Continue(string terminalNodeId) => new(NavigationSignalKind.Continue, TerminalNodeId: terminalNodeId);

        public static NavigationSignal LoopIterationCompleted(string terminalNodeId) => new(NavigationSignalKind.LoopIterationCompleted, TerminalNodeId: terminalNodeId);

        public static NavigationSignal ContinueWith(MicroflowExecutionFlow? nextFlow) => new(NavigationSignalKind.ContinueWith, NextFlow: nextFlow);
    }
}
