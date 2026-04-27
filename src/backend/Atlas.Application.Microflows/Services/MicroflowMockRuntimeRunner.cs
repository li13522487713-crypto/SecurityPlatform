using System.Globalization;
using System.Text.Json;
using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Runtime.Actions;
using Atlas.Application.Microflows.Runtime.Calls;
using Atlas.Application.Microflows.Runtime.Expressions;
using Atlas.Application.Microflows.Runtime.Transactions;

namespace Atlas.Application.Microflows.Services;

public sealed class MicroflowMockRuntimeRunner : IMicroflowMockRuntimeRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IMicroflowSchemaReader _schemaReader;
    private readonly IMicroflowClock _clock;
    private readonly IMicroflowExpressionEvaluator _expressionEvaluator;
    private readonly IMicroflowTransactionManager _transactionManager;
    private readonly IMicroflowActionExecutorRegistry _actionExecutorRegistry;
    private readonly IMicroflowRuntimeConnectorRegistry _connectorRegistry;

    public MicroflowMockRuntimeRunner(
        IMicroflowSchemaReader schemaReader,
        IMicroflowClock clock,
        IMicroflowExpressionEvaluator expressionEvaluator,
        IMicroflowTransactionManager transactionManager,
        IMicroflowActionExecutorRegistry actionExecutorRegistry,
        IMicroflowRuntimeConnectorRegistry connectorRegistry)
    {
        _schemaReader = schemaReader;
        _clock = clock;
        _expressionEvaluator = expressionEvaluator;
        _transactionManager = transactionManager;
        _actionExecutorRegistry = actionExecutorRegistry;
        _connectorRegistry = connectorRegistry;
    }

    public Task<MicroflowRunSessionDto> RunAsync(MicroflowMockRuntimeRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var context = new MockRuntimeContext(request, _schemaReader.Read(request.Schema), _clock, _expressionEvaluator, _transactionManager, _actionExecutorRegistry, _connectorRegistry);
        context.SeedInputVariables();

        var start = context.Model.Objects.SingleOrDefault(o => !o.InsideLoop && IsKind(o, "startEvent"));
        if (start is null)
        {
            return Task.FromResult(context.BuildSession("failed", RuntimeErrorCode.RuntimeStartNotFound, "未找到 root StartEvent。"));
        }

        var signal = ExecutePath(context, start.Id, incomingFlowId: null, loopIteration: null, cancellationToken);
        var status = signal.Kind == ExecutionSignalKind.Success ? "success" : "failed";
        var error = signal.Error;
        if (signal.Kind != ExecutionSignalKind.Success && error is null)
        {
            error = new MicroflowRuntimeErrorDto
            {
                Code = RuntimeErrorCode.RuntimeEndNotReached,
                Message = "微流未到达 EndEvent。"
            };
        }

        return Task.FromResult(context.BuildSession(status, error));
    }

    private static ExecutionSignal ExecutePath(
        MockRuntimeContext context,
        string objectId,
        string? incomingFlowId,
        JsonElement? loopIteration,
        CancellationToken cancellationToken)
    {
        var currentObjectId = objectId;
        var incoming = incomingFlowId;
        while (!string.IsNullOrWhiteSpace(currentObjectId))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!context.TryStep(out var maxStepsError))
            {
                return ExecutionSignal.Failed(maxStepsError);
            }

            if (!context.Objects.TryGetValue(currentObjectId, out var obj))
            {
                return ExecutionSignal.Failed(new MicroflowRuntimeErrorDto
                {
                    Code = RuntimeErrorCode.RuntimeObjectNotFound,
                    Message = $"运行对象不存在：{currentObjectId}",
                    FlowId = incoming
                });
            }

            if (IsKind(obj, "annotation") || IsKind(obj, "parameterObject"))
            {
                var next = context.NextNormalFlow(obj.Id);
                currentObjectId = next?.DestinationObjectId;
                incoming = next?.Id;
                continue;
            }

            if (IsKind(obj, "startEvent") || IsKind(obj, "exclusiveMerge"))
            {
                var next = context.NextNormalFlow(obj.Id);
                context.AddFrame(obj, incoming, next?.Id, "success", input: FrameInput(obj), output: JsonObj(new { nextObjectId = next?.DestinationObjectId }), loopIteration: loopIteration);
                if (next is null)
                {
                    return ExecutionSignal.Failed(context.EndNotReached(obj, incoming));
                }

                currentObjectId = next.DestinationObjectId!;
                incoming = next.Id;
                continue;
            }

            if (IsKind(obj, "endEvent"))
            {
                var output = BuildEndOutput(context, obj);
                context.AddFrame(obj, incoming, null, "success", input: FrameInput(obj), output: output, loopIteration: loopIteration, message: "EndEvent reached.");
                return ExecutionSignal.Success(output);
            }

            if (IsKind(obj, "errorEvent"))
            {
                var error = new MicroflowRuntimeErrorDto
                {
                    Code = RuntimeErrorCode.RuntimeUnknownError,
                    Message = obj.Caption ?? "Microflow ErrorEvent reached.",
                    ObjectId = obj.Id,
                    FlowId = incoming
                };
                context.AddFrame(obj, incoming, null, "failed", input: FrameInput(obj), error: error, loopIteration: loopIteration, message: "ErrorEvent reached.");
                return ExecutionSignal.Failed(error);
            }

            if (IsKind(obj, "breakEvent"))
            {
                if (context.RuntimeContext.LoopStack.Count == 0)
                {
                    var error = new MicroflowRuntimeErrorDto
                    {
                        Code = RuntimeErrorCode.RuntimeLoopControlOutOfScope,
                        Message = "BreakEvent is only valid inside loop context.",
                        ObjectId = obj.Id,
                        FlowId = incoming
                    };
                    context.AddFrame(obj, incoming, null, "failed", input: FrameInput(obj), error: error, loopIteration: loopIteration);
                    return ExecutionSignal.Failed(error);
                }

                context.AddFrame(obj, incoming, null, "success", input: FrameInput(obj), output: JsonObj(new { signal = "break" }), loopIteration: WithLoopControl(loopIteration, "break"), message: "Break loop.");
                return ExecutionSignal.Break();
            }

            if (IsKind(obj, "continueEvent"))
            {
                if (context.RuntimeContext.LoopStack.Count == 0)
                {
                    var error = new MicroflowRuntimeErrorDto
                    {
                        Code = RuntimeErrorCode.RuntimeLoopControlOutOfScope,
                        Message = "ContinueEvent is only valid inside loop context.",
                        ObjectId = obj.Id,
                        FlowId = incoming
                    };
                    context.AddFrame(obj, incoming, null, "failed", input: FrameInput(obj), error: error, loopIteration: loopIteration);
                    return ExecutionSignal.Failed(error);
                }

                context.AddFrame(obj, incoming, null, "success", input: FrameInput(obj), output: JsonObj(new { signal = "continue" }), loopIteration: WithLoopControl(loopIteration, "continue"), message: "Continue loop.");
                return ExecutionSignal.Continue();
            }

            if (IsKind(obj, "exclusiveSplit"))
            {
                var decision = SelectDecisionValue(context, obj);
                var selectedValue = decision.SelectedValue;
                var selected = context.SelectCaseFlow(obj.Id, selectedValue);
                if (selected.Flow is null)
                {
                    var error = InvalidCase(obj, selectedValue);
                    context.AddFrame(obj, incoming, null, "failed", input: FrameInput(obj), error: error, selectedCaseValue: selected.CaseValue, loopIteration: loopIteration);
                    return ExecutionSignal.Failed(error);
                }

                context.AddFrame(obj, incoming, selected.Flow.Id, "success", input: FrameInput(obj), output: JsonObj(new { selectedCaseValue = selectedValue, expressionResult = decision.ExpressionResult }), selectedCaseValue: selected.CaseValue, loopIteration: loopIteration);
                currentObjectId = selected.Flow.DestinationObjectId!;
                incoming = selected.Flow.Id;
                continue;
            }

            if (IsKind(obj, "inheritanceSplit"))
            {
                var selectedValue = context.Options.ObjectTypeCase ?? "fallback";
                var selected = context.SelectCaseFlow(obj.Id, selectedValue);
                if (selected.Flow is null)
                {
                    var error = InvalidCase(obj, selectedValue);
                    context.AddFrame(obj, incoming, null, "failed", input: FrameInput(obj), error: error, selectedCaseValue: selected.CaseValue, loopIteration: loopIteration);
                    return ExecutionSignal.Failed(error);
                }

                context.AddFrame(obj, incoming, selected.Flow.Id, "success", input: FrameInput(obj), output: JsonObj(new { selectedCaseValue = selectedValue }), selectedCaseValue: selected.CaseValue, loopIteration: loopIteration);
                currentObjectId = selected.Flow.DestinationObjectId!;
                incoming = selected.Flow.Id;
                continue;
            }

            if (IsKind(obj, "loopedActivity"))
            {
                var loopSignal = ExecuteLoop(context, obj, incoming, loopIteration, cancellationToken);
                if (loopSignal.Kind == ExecutionSignalKind.Error)
                {
                    return loopSignal;
                }

                var next = context.NextNormalFlow(obj.Id);
                if (next is null)
                {
                    return ExecutionSignal.Failed(context.EndNotReached(obj, incoming));
                }

                currentObjectId = next.DestinationObjectId!;
                incoming = next.Id;
                continue;
            }

            if (IsKind(obj, "actionActivity"))
            {
                var outcome = ExecuteAction(context, obj, incoming, loopIteration, cancellationToken);
                if (!outcome.Succeeded)
                {
                    var actionError = outcome.Error ?? UnknownActionError(obj, incoming);
                    var handling = ReadErrorHandling(obj.Action?.Raw);
                    var errorFlow = context.ErrorHandlerFlow(obj.Id);
                    if (string.Equals(handling, "continue", StringComparison.OrdinalIgnoreCase))
                    {
                        context.TransactionManager.ContinueAfterError(context.RuntimeContext, actionError);
                        context.DrainTransactionLogs();
                        context.AddLog("warning", obj.Id, obj.Action?.Id, $"Action failed and continued: {actionError.Code}");
                        var continued = context.NextNormalFlow(obj.Id);
                        if (continued is null)
                        {
                            return ExecutionSignal.Failed(context.EndNotReached(obj, incoming));
                        }

                        currentObjectId = continued.DestinationObjectId!;
                        incoming = continued.Id;
                        continue;
                    }

                    if ((string.Equals(handling, "customWithRollback", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(handling, "customWithoutRollback", StringComparison.OrdinalIgnoreCase))
                        && errorFlow is not null)
                    {
                        if (string.Equals(handling, "customWithRollback", StringComparison.OrdinalIgnoreCase))
                        {
                            context.TransactionManager.PrepareCustomWithRollback(context.RuntimeContext, actionError);
                            context.DrainTransactionLogs();
                            context.AddLog("warning", obj.Id, obj.Action?.Id, "Mock transaction rolled back before custom error handler.");
                        }
                        else
                        {
                            context.TransactionManager.PrepareCustomWithoutRollback(context.RuntimeContext, actionError);
                            context.DrainTransactionLogs();
                        }

                        using var errorScope = context.PushErrorHandlerScope(actionError, errorFlow.Id, outcome.LatestHttpResponse);
                        var handled = ExecutePath(context, errorFlow.DestinationObjectId!, errorFlow.Id, loopIteration, cancellationToken);
                        return handled.Kind == ExecutionSignalKind.Error ? handled : ExecutionSignal.Success(handled.Output);
                    }

                    context.TransactionManager.RollbackForError(context.RuntimeContext, actionError, obj.Id, obj.Action?.Id);
                    context.DrainTransactionLogs();
                    return ExecutionSignal.Failed(actionError);
                }

                var next = context.NextNormalFlow(obj.Id);
                if (next is null)
                {
                    return ExecutionSignal.Failed(context.EndNotReached(obj, incoming));
                }

                currentObjectId = next.DestinationObjectId!;
                incoming = next.Id;
                continue;
            }

            var unsupported = new MicroflowRuntimeErrorDto
            {
                Code = RuntimeErrorCode.RuntimeUnsupportedAction,
                Message = $"运行对象类型暂不支持：{obj.Kind}",
                ObjectId = obj.Id,
                FlowId = incoming
            };
            context.AddFrame(obj, incoming, null, "failed", input: FrameInput(obj), error: unsupported, loopIteration: loopIteration);
            return ExecutionSignal.Failed(unsupported);
        }

        return ExecutionSignal.Failed(new MicroflowRuntimeErrorDto
        {
            Code = RuntimeErrorCode.RuntimeEndNotReached,
            Message = "微流执行路径为空。"
        });
    }

    private static ExecutionSignal ExecuteLoop(
        MockRuntimeContext context,
        MicroflowObjectModel loop,
        string? incomingFlowId,
        JsonElement? outerLoopIteration,
        CancellationToken cancellationToken)
    {
        var start = context.FirstLoopObject(loop.Id);
        if (start is null)
        {
            var error = new MicroflowRuntimeErrorDto
            {
                Code = RuntimeErrorCode.RuntimeLoopBodyNotFound,
                Message = "Loop internal entry node is missing.",
                ObjectId = loop.Id,
                FlowId = incomingFlowId
            };
            context.AddFrame(loop, incomingFlowId, null, "failed", input: FrameInput(loop), error: error, loopIteration: outerLoopIteration);
            return ExecutionSignal.Failed(error);
        }

        var source = loop.Raw.TryGetProperty("loopSource", out var loopSource) ? loopSource : default;
        var kind = ReadString(source, "kind") ?? (!string.IsNullOrWhiteSpace(ReadExpressionText(source, "expression")) ? "whileCondition" : "iterableList");
        var maxIterations = Math.Clamp(context.Options.LoopIterations ?? ReadInt(source, "maxIterations") ?? 100, 1, 5000);
        return string.Equals(kind, "whileCondition", StringComparison.OrdinalIgnoreCase)
            ? ExecuteWhileLoop(context, loop, start, incomingFlowId, outerLoopIteration, source, maxIterations, cancellationToken)
            : ExecuteIterableLoop(context, loop, start, incomingFlowId, outerLoopIteration, source, maxIterations, cancellationToken);
    }

    private static ExecutionSignal ExecuteIterableLoop(
        MockRuntimeContext context,
        MicroflowObjectModel loop,
        MicroflowObjectModel start,
        string? incomingFlowId,
        JsonElement? outerLoopIteration,
        JsonElement source,
        int maxIterations,
        CancellationToken cancellationToken)
    {
        var listVariableName = ReadString(source, "listVariableName");
        if (string.IsNullOrWhiteSpace(listVariableName) || !context.RuntimeContext.VariableStore.TryGet(listVariableName!, out var listVariable) || listVariable is null)
        {
            var error = new MicroflowRuntimeErrorDto
            {
                Code = string.IsNullOrWhiteSpace(listVariableName) ? RuntimeErrorCode.RuntimeLoopSourceNotFound : RuntimeErrorCode.RuntimeVariableNotFound,
                Message = string.IsNullOrWhiteSpace(listVariableName) ? "Loop listVariableName is missing." : $"Loop list variable '{listVariableName}' was not found.",
                ObjectId = loop.Id,
                FlowId = incomingFlowId
            };
            context.AddFrame(loop, incomingFlowId, null, "failed", input: FrameInput(loop), error: error, loopIteration: outerLoopIteration);
            return ExecutionSignal.Failed(error);
        }

        if (!TryReadListItems(listVariable, out var items, out var itemTypeJson))
        {
            var error = new MicroflowRuntimeErrorDto
            {
                Code = RuntimeErrorCode.RuntimeLoopSourceNotList,
                Message = $"Loop source '{listVariableName}' is not a list.",
                ObjectId = loop.Id,
                FlowId = incomingFlowId
            };
            context.AddFrame(loop, incomingFlowId, null, "failed", input: FrameInput(loop), error: error, loopIteration: outerLoopIteration);
            return ExecutionSignal.Failed(error);
        }

        var iteratorName = ReadString(source, "iteratorVariableName");
        if (string.IsNullOrWhiteSpace(iteratorName))
        {
            var error = new MicroflowRuntimeErrorDto
            {
                Code = RuntimeErrorCode.RuntimeLoopIteratorInvalid,
                Message = "Loop iteratorVariableName is missing.",
                ObjectId = loop.Id,
                FlowId = incomingFlowId
            };
            context.AddFrame(loop, incomingFlowId, null, "failed", input: FrameInput(loop), error: error, loopIteration: outerLoopIteration);
            return ExecutionSignal.Failed(error);
        }

        context.AddFrame(
            loop,
            incomingFlowId,
            context.NextNormalFlow(loop.Id)?.Id,
            "success",
            input: FrameInput(loop),
            output: JsonObj(new { mode = "iterableList", listVariableName, iteratorVariableName = iteratorName, itemCount = items.Count }),
            loopIteration: outerLoopIteration,
            message: $"Loop iterableList itemCount={items.Count}.");

        if (items.Count == 0)
        {
            return ExecutionSignal.Success();
        }

        var loopCollectionId = start.CollectionId ?? loop.CollectionId;
        for (var index = 0; index < items.Count; index++)
        {
            if (index >= maxIterations)
            {
                return ExecutionSignal.Failed(MaxIterationsError(loop, incomingFlowId, maxIterations));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var item = items[index];
            var iteratorPreview = Preview(item);
            var iterationJson = LoopIterationJson(loop, loopCollectionId, index, iteratorName, iteratorPreview, context.RuntimeContext.LoopStack.Count + 1, itemCount: items.Count);
            using (context.PushLoopScope(loop.Id, loopCollectionId, iteratorName!, index, item, iteratorPreview, itemTypeJson, defineIterator: true))
            {
                var signal = ExecutePath(context, start.Id, incomingFlowId: null, loopIteration: iterationJson, cancellationToken);
                if (signal.Kind == ExecutionSignalKind.Error)
                {
                    return signal;
                }

                if (signal.Kind == ExecutionSignalKind.Break)
                {
                    break;
                }
            }
        }

        return ExecutionSignal.Success();
    }

    private static ExecutionSignal ExecuteWhileLoop(
        MockRuntimeContext context,
        MicroflowObjectModel loop,
        MicroflowObjectModel start,
        string? incomingFlowId,
        JsonElement? outerLoopIteration,
        JsonElement source,
        int maxIterations,
        CancellationToken cancellationToken)
    {
        var expression = ReadExpressionText(source, "expression");
        if (string.IsNullOrWhiteSpace(expression))
        {
            var error = new MicroflowRuntimeErrorDto
            {
                Code = RuntimeErrorCode.RuntimeLoopConditionError,
                Message = "While loop expression is missing.",
                ObjectId = loop.Id,
                FlowId = incomingFlowId
            };
            context.AddFrame(loop, incomingFlowId, null, "failed", input: FrameInput(loop), error: error, loopIteration: outerLoopIteration);
            return ExecutionSignal.Failed(error);
        }

        context.AddFrame(
            loop,
            incomingFlowId,
            context.NextNormalFlow(loop.Id)?.Id,
            "success",
            input: FrameInput(loop),
            output: JsonObj(new { mode = "whileCondition", expression, maxIterations }),
            loopIteration: outerLoopIteration,
            message: "Loop whileCondition started.");

        var iteratorName = ReadString(source, "iteratorVariableName");
        var loopCollectionId = start.CollectionId ?? loop.CollectionId;
        var index = 0;
        while (true)
        {
            if (index >= maxIterations)
            {
                return ExecutionSignal.Failed(MaxIterationsError(loop, incomingFlowId, maxIterations));
            }

            cancellationToken.ThrowIfCancellationRequested();
            MicroflowExpressionEvaluationResult condition;
            try
            {
                condition = context.EvaluateExpressionOrThrow(expression!, loop, loop.Action, MicroflowExpressionType.Simple(MicroflowExpressionTypeKind.Boolean));
            }
            catch (MicroflowExpressionRuntimeFailure ex)
            {
                var error = ex.Error with { Code = RuntimeErrorCode.RuntimeLoopConditionError, ObjectId = loop.Id, FlowId = incomingFlowId };
                context.AddFrame(loop, incomingFlowId, null, "failed", input: FrameInput(loop), output: ex.Output, error: error, loopIteration: outerLoopIteration);
                return ExecutionSignal.Failed(error);
            }

            if (condition.Value?.BoolValue is null)
            {
                var error = new MicroflowRuntimeErrorDto
                {
                    Code = RuntimeErrorCode.RuntimeLoopConditionNotBoolean,
                    Message = "While loop condition did not evaluate to boolean.",
                    ObjectId = loop.Id,
                    FlowId = incomingFlowId
                };
                context.AddFrame(loop, incomingFlowId, null, "failed", input: FrameInput(loop), error: error, loopIteration: outerLoopIteration);
                return ExecutionSignal.Failed(error);
            }

            if (condition.Value.BoolValue != true)
            {
                return ExecutionSignal.Success();
            }

            var iterationJson = LoopIterationJson(loop, loopCollectionId, index, iteratorName, iteratorName is null ? null : $"{iteratorName}[{index}]", context.RuntimeContext.LoopStack.Count + 1, conditionResult: true);
            using (context.PushLoopScope(loop.Id, loopCollectionId, iteratorName, index, JsonObj(new { index }), iteratorName is null ? null : $"{iteratorName}[{index}]", iteratorDataTypeJson: null, defineIterator: iteratorName is not null))
            {
                var signal = ExecutePath(context, start.Id, incomingFlowId: null, loopIteration: iterationJson, cancellationToken);
                index++;
                if (signal.Kind == ExecutionSignalKind.Error)
                {
                    return signal;
                }

                if (signal.Kind == ExecutionSignalKind.Break)
                {
                    break;
                }
            }
        }

        return ExecutionSignal.Success();
    }

    private static JsonElement LoopIterationJson(
        MicroflowObjectModel loop,
        string? collectionId,
        int index,
        string? iteratorName,
        string? iteratorPreview,
        int depth,
        bool? conditionResult = null,
        int? itemCount = null,
        string controlSignal = "none")
        => JsonObj(new
        {
            loopObjectId = loop.Id,
            collectionId,
            index,
            iteratorVariableName = iteratorName,
            iteratorValuePreview = iteratorPreview,
            parentLoopObjectId = loop.ParentLoopObjectId,
            depth,
            controlSignal,
            conditionResult,
            itemCount
        });

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

        return JsonObj(values);
    }

    private static MicroflowRuntimeErrorDto MaxIterationsError(MicroflowObjectModel loop, string? incomingFlowId, int maxIterations)
        => new()
        {
            Code = RuntimeErrorCode.RuntimeLoopMaxIterationsExceeded,
            Message = $"Loop exceeded maxIterations={maxIterations}.",
            ObjectId = loop.Id,
            FlowId = incomingFlowId
        };

    private static bool TryReadListItems(MicroflowRuntimeVariableValue variable, out IReadOnlyList<JsonElement> items, out string? itemTypeJson)
    {
        itemTypeJson = ReadItemTypeJson(variable.DataTypeJson);
        items = Array.Empty<JsonElement>();
        if (!string.Equals(variable.Kind, MicroflowRuntimeVariableKind.List, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(ReadTypeKind(variable.DataTypeJson), "list", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(variable.RawValueJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(variable.RawValueJson);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                items = document.RootElement.EnumerateArray().Select(item => item.Clone()).ToArray();
                return true;
            }

            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("items", out var objectItems)
                && objectItems.ValueKind == JsonValueKind.Array)
            {
                items = objectItems.EnumerateArray().Select(item => item.Clone()).ToArray();
                return true;
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static ActionOutcome ExecuteAction(
        MockRuntimeContext context,
        MicroflowObjectModel obj,
        string? incomingFlowId,
        JsonElement? loopIteration,
        CancellationToken cancellationToken)
    {
        var action = obj.Action;
        var executor = context.ActionExecutorRegistry.GetOrFallback(action?.Kind);
        if (action is null || string.IsNullOrWhiteSpace(action.Kind) || ReadBool(action.Raw, "modeledOnly"))
        {
            return ExecuteRegistryOnlyAction(context, obj, action, executor, incomingFlowId, loopIteration, cancellationToken);
        }

        if (executor.Category is MicroflowActionRuntimeCategory.RuntimeCommand
            or MicroflowActionRuntimeCategory.ConnectorBacked
            or MicroflowActionRuntimeCategory.ExplicitUnsupported)
        {
            return ExecuteRegistryOnlyAction(context, obj, action, executor, incomingFlowId, loopIteration, cancellationToken);
        }

        if (string.Equals(action.Kind, "callMicroflow", StringComparison.OrdinalIgnoreCase)
            || string.Equals(action.Kind, "restCall", StringComparison.OrdinalIgnoreCase)
            || string.Equals(action.Kind, "logMessage", StringComparison.OrdinalIgnoreCase))
        {
            return ExecuteRegistryOnlyAction(context, obj, action, executor, incomingFlowId, loopIteration, cancellationToken);
        }

        if (string.Equals(action.Kind, "restCall", StringComparison.OrdinalIgnoreCase) && context.Options.SimulateRestError == true)
        {
            var requestPreview = BuildRestRequestPreview(context, obj, action);
            var error = new MicroflowRuntimeErrorDto
            {
                Code = RuntimeErrorCode.RuntimeRestCallFailed,
                Message = "Mock REST call failed by simulateRestError.",
                ObjectId = obj.Id,
                ActionId = action.Id,
                FlowId = incomingFlowId,
                Details = requestPreview.GetRawText()
            };
            var latestHttpResponse = JsonObj(new { statusCode = 500, body = "mock-rest-error", headers = new { contentType = "application/json" } });
            context.AddFrame(obj, incomingFlowId, context.ErrorHandlerFlow(obj.Id)?.Id, "failed", input: FrameInput(obj), output: JsonObj(new { requestPreview }), error: error, loopIteration: loopIteration, message: "REST error path mocked.");
            return ActionOutcome.Failed(error, latestHttpResponse);
        }

        JsonElement output;
        try
        {
            output = action.Kind switch
            {
                "retrieve" => MockRetrieve(context, obj, action),
                "createObject" => MockCreateObject(context, obj, action),
                "changeMembers" => MockChangeMembers(context, obj, action),
                "commit" => MockCommit(context, obj, action),
                "delete" => MockDelete(context, obj, action),
                "rollback" => MockRollback(context, obj, action),
                "createVariable" => MockCreateVariable(context, obj, action),
                "changeVariable" => MockChangeVariable(context, obj, action),
                "restCall" => MockRestCall(context, obj, action),
                "logMessage" => MockLogMessage(context, obj, action),
                "cast" => MockCastObject(context, obj, action),
                "createList" => MockCreateList(context, obj, action),
                "changeList" => MockChangeList(context, obj, action),
                "listOperation" => MockListOperation(context, obj, action),
                "aggregateList" => MockAggregateList(context, obj, action),
                "counter" or "incrementCounter" or "gauge" or "metrics" => MockMetrics(context, obj, action),
                _ => JsonObj(new { actionKind = action.Kind, executorCategory = executor.Category, mocked = true })
            };
        }
        catch (MicroflowExpressionRuntimeFailure ex)
        {
            context.AddFrame(obj, incomingFlowId, null, "failed", input: FrameInput(obj), output: ex.Output, error: ex.Error, loopIteration: loopIteration);
            return ActionOutcome.Failed(ex.Error);
        }
        catch (MicroflowVariableStoreException ex)
        {
            var variableError = new MicroflowRuntimeErrorDto
            {
                Code = ex.Diagnostic.Code,
                Message = ex.Diagnostic.Message,
                ObjectId = obj.Id,
                ActionId = action.Id,
                FlowId = incomingFlowId
            };
            context.AddFrame(obj, incomingFlowId, null, "failed", input: FrameInput(obj), error: variableError, loopIteration: loopIteration);
            return ActionOutcome.Failed(variableError);
        }

        if (action.Kind == "changeVariable" && output.ValueKind == JsonValueKind.Undefined)
        {
            var error = new MicroflowRuntimeErrorDto
            {
                Code = RuntimeErrorCode.RuntimeVariableNotFound,
                Message = $"变量不存在：{ReadString(action.Raw, "targetVariableName")}",
                ObjectId = obj.Id,
                ActionId = action.Id,
                FlowId = incomingFlowId
            };
            context.AddFrame(obj, incomingFlowId, null, "failed", input: FrameInput(obj), error: error, loopIteration: loopIteration);
            return ActionOutcome.Failed(error);
        }

        context.DrainTransactionLogs();
        context.AddFrame(obj, incomingFlowId, context.NextNormalFlow(obj.Id)?.Id, "success", input: FrameInput(obj), output: context.WithActionExecutionPreview(output, action.Kind, executor), loopIteration: loopIteration);
        return ActionOutcome.Success();
    }

    private static ActionOutcome ExecuteRegistryOnlyAction(
        MockRuntimeContext context,
        MicroflowObjectModel obj,
        MicroflowActionModel? action,
        IMicroflowActionExecutor executor,
        string? incomingFlowId,
        JsonElement? loopIteration,
        CancellationToken cancellationToken)
    {
        var node = context.RuntimeContext.ExecutionPlan.Nodes.FirstOrDefault(node => node.ObjectId == obj.Id)
            ?? new MicroflowExecutionNode
            {
                ObjectId = obj.Id,
                ActionId = action?.Id,
                CollectionId = obj.CollectionId,
                Kind = obj.Kind,
                ActionKind = action?.Kind,
                ConfigJson = action?.Raw
            };
        var result = executor.ExecuteAsync(
            new MicroflowActionExecutionContext
            {
                RuntimeExecutionContext = context.RuntimeContext,
                ExecutionPlan = context.RuntimeContext.ExecutionPlan,
                ExecutionNode = node,
                ActionConfig = action?.Raw ?? default,
                ActionKind = action?.Kind ?? "unknown",
                ObjectId = obj.Id,
                ActionId = action?.Id,
                CollectionId = obj.CollectionId,
                VariableStore = context.RuntimeContext.VariableStore,
                ExpressionEvaluator = context.ExpressionEvaluator,
                MetadataCatalog = context.Metadata,
                TransactionManager = context.TransactionManager,
                ConnectorRegistry = context.ConnectorRegistry,
                RuntimeSecurityContext = context.RuntimeContext.RuntimeSecurityContext,
                Options = new MicroflowActionExecutionOptions
                {
                    Mode = MicroflowRuntimeExecutionMode.TestRun,
                    MaxCallDepth = context.RuntimeContext.MaxCallDepth,
                    AllowRealHttp = context.Options.AllowRealHttp == true,
                    SimulateRestError = context.Options.SimulateRestError == true,
                    ConnectorCapabilities = context.ConnectorRegistry.ListEnabledCapabilities()
                }
            },
            cancellationToken).GetAwaiter().GetResult();
        var output = result.OutputJson ?? JsonObj(new
        {
            actionKind = action?.Kind ?? "missing",
            executorCategory = executor.Category,
            supportLevel = executor.SupportLevel,
            outputPreview = result.OutputPreview,
            runtimeCommands = result.RuntimeCommands,
            connectorRequests = result.ConnectorRequests,
            diagnostics = result.Diagnostics
        });
        context.AddFrame(
            obj,
            incomingFlowId,
            result.Error is null ? context.NextNormalFlow(obj.Id)?.Id : null,
            result.Error is null ? "success" : "failed",
            input: FrameInput(obj),
            output: context.WithActionExecutionPreview(output, action?.Kind ?? "unknown", executor, result),
            error: result.Error,
            loopIteration: loopIteration,
            message: result.Message);
        context.AddLogs(result.Logs);
        context.AddChildRuns(result.ChildRunSessions);
        return result.Error is null
            ? ActionOutcome.Success()
            : ActionOutcome.Failed(result.Error, result.LatestHttpResponse);
    }

    private static JsonElement MockRetrieve(MockRuntimeContext context, MicroflowObjectModel obj, MicroflowActionModel action)
    {
        var variableName = ReadString(action.Raw, "outputVariableName") ?? "retrievedObjects";
        var entity = ReadStringByPath(action.Raw, "retrieveSource", "entityQualifiedName") ?? "Mock.Entity";
        var value = JsonObj(new { items = new[] { new { id = "mock-object-1", entityQualifiedName = entity } }, count = 1 });
        context.SetVariable(variableName, Type("list"), value, "retrieve", obj.Id, action.Id, obj.CollectionId);
        return JsonObj(new { outputVariableName = variableName, entityQualifiedName = entity, count = 1 });
    }

    private static JsonElement MockCreateObject(MockRuntimeContext context, MicroflowObjectModel obj, MicroflowActionModel action)
    {
        var variableName = ReadString(action.Raw, "outputVariableName") ?? "createdObject";
        var entity = ReadString(action.Raw, "entityQualifiedName") ?? "Mock.Entity";
        var members = ReadMemberChanges(action.Raw);
        var changes = members.Count;
        var runtimeObjectId = $"runtime-object-{Guid.NewGuid():N}";
        var value = JsonObj(new { id = runtimeObjectId, entityQualifiedName = entity, changedMembers = changes });
        context.SetVariable(variableName, Type("object"), value, "createObject", obj.Id, action.Id, obj.CollectionId);
        context.TransactionManager.TrackCreate(
            context.RuntimeContext,
            new MicroflowRuntimeObjectChangeInput
            {
                EntityQualifiedName = entity,
                ObjectId = runtimeObjectId,
                VariableName = variableName,
                SourceObjectId = obj.Id,
                SourceActionId = action.Id,
                CollectionId = obj.CollectionId,
                AfterJson = value.GetRawText(),
                ChangedMembers = members,
                WithEvents = ReadBool(action.Raw, "withEvents"),
                RefreshInClient = ReadBool(action.Raw, "refreshInClient"),
                ValidateObject = ReadBool(action.Raw, "validateObject"),
                Preview = $"create {entity} -> ${variableName}"
            });
        if (ReadBoolByPath(action.Raw, "commit", "enabled"))
        {
            context.TransactionManager.TrackCommitAction(
                context.RuntimeContext,
                new MicroflowRuntimeCommitActionInput
                {
                    ObjectOrListVariableName = variableName,
                    SourceObjectId = obj.Id,
                    SourceActionId = action.Id,
                    CollectionId = obj.CollectionId,
                    WithEvents = ReadBool(action.Raw, "withEvents"),
                    RefreshInClient = ReadBool(action.Raw, "refreshInClient"),
                    Reason = "implicit createObject commit"
                });
        }

        return JsonObj(new { outputVariableName = variableName, entityQualifiedName = entity, changedMembers = changes });
    }

    private static JsonElement MockChangeMembers(MockRuntimeContext context, MicroflowObjectModel obj, MicroflowActionModel action)
    {
        var variableName = ReadString(action.Raw, "changeVariableName") ?? ReadString(action.Raw, "objectVariableName");
        var before = !string.IsNullOrWhiteSpace(variableName) && context.Variables.TryGetValue(variableName!, out var value)
            ? value.RawValueJson
            : null;
        var members = ReadMemberChanges(action.Raw);
        var entity = TryReadEntityFromVariable(context, variableName) ?? ReadString(action.Raw, "entityQualifiedName") ?? "Mock.Entity";
        var after = JsonObj(new { variableName, entityQualifiedName = entity, changedMembers = members.Count, mockedUpdate = true });
        context.TransactionManager.TrackUpdate(
            context.RuntimeContext,
            new MicroflowRuntimeObjectChangeInput
            {
                EntityQualifiedName = entity,
                ObjectId = TryReadObjectIdFromVariable(context, variableName),
                VariableName = variableName,
                SourceObjectId = obj.Id,
                SourceActionId = action.Id,
                CollectionId = obj.CollectionId,
                BeforeJson = before,
                AfterJson = after.GetRawText(),
                ChangedMembers = members,
                WithEvents = ReadBool(action.Raw, "withEvents"),
                RefreshInClient = ReadBool(action.Raw, "refreshInClient"),
                ValidateObject = ReadBool(action.Raw, "validateObject"),
                Preview = $"update {entity} from ${variableName ?? "unknown"}"
            });
        if (ReadBoolByPath(action.Raw, "commit", "enabled"))
        {
            context.TransactionManager.TrackCommitAction(
                context.RuntimeContext,
                new MicroflowRuntimeCommitActionInput
                {
                    ObjectOrListVariableName = variableName,
                    SourceObjectId = obj.Id,
                    SourceActionId = action.Id,
                    CollectionId = obj.CollectionId,
                    WithEvents = ReadBool(action.Raw, "withEvents"),
                    RefreshInClient = ReadBool(action.Raw, "refreshInClient"),
                    Reason = "implicit changeMembers commit"
                });
        }

        return JsonObj(new { variableName, changedMembers = members.Count });
    }

    private static JsonElement MockCommit(MockRuntimeContext context, MicroflowObjectModel obj, MicroflowActionModel action)
    {
        var variableName = ReadString(action.Raw, "objectOrListVariableName");
        context.TransactionManager.TrackCommitAction(
            context.RuntimeContext,
            new MicroflowRuntimeCommitActionInput
            {
                ObjectOrListVariableName = variableName,
                SourceObjectId = obj.Id,
                SourceActionId = action.Id,
                CollectionId = obj.CollectionId,
                WithEvents = ReadBool(action.Raw, "withEvents"),
                RefreshInClient = ReadBool(action.Raw, "refreshInClient"),
                Reason = "CommitAction"
            });
        return JsonObj(new { committed = true, variableName });
    }

    private static JsonElement MockDelete(MockRuntimeContext context, MicroflowObjectModel obj, MicroflowActionModel action)
    {
        var variableName = ReadString(action.Raw, "objectOrListVariableName");
        var before = !string.IsNullOrWhiteSpace(variableName) && context.Variables.TryGetValue(variableName!, out var value)
            ? value.RawValueJson
            : null;
        var entity = TryReadEntityFromVariable(context, variableName) ?? ReadString(action.Raw, "entityQualifiedName") ?? "Mock.Entity";
        context.TransactionManager.TrackDelete(
            context.RuntimeContext,
            new MicroflowRuntimeObjectChangeInput
            {
                EntityQualifiedName = entity,
                ObjectId = TryReadObjectIdFromVariable(context, variableName),
                VariableName = variableName,
                SourceObjectId = obj.Id,
                SourceActionId = action.Id,
                CollectionId = obj.CollectionId,
                BeforeJson = before,
                WithEvents = ReadBool(action.Raw, "withEvents"),
                Preview = $"delete {entity} from ${variableName ?? "unknown"}"
            });
        return JsonObj(new { deleted = true, variableName, deleteBehavior = ReadString(action.Raw, "deleteBehavior") });
    }

    private static JsonElement MockRollback(MockRuntimeContext context, MicroflowObjectModel obj, MicroflowActionModel action)
    {
        var variableName = ReadString(action.Raw, "objectOrListVariableName");
        var entity = TryReadEntityFromVariable(context, variableName) ?? ReadString(action.Raw, "entityQualifiedName") ?? "Mock.Entity";
        context.TransactionManager.TrackRollbackObject(
            context.RuntimeContext,
            new MicroflowRuntimeObjectChangeInput
            {
                EntityQualifiedName = entity,
                ObjectId = TryReadObjectIdFromVariable(context, variableName),
                VariableName = variableName,
                SourceObjectId = obj.Id,
                SourceActionId = action.Id,
                CollectionId = obj.CollectionId,
                RefreshInClient = ReadBool(action.Raw, "refreshInClient"),
                Preview = $"rollback object {entity} from ${variableName ?? "unknown"}"
            });
        return JsonObj(new { rolledBack = true, variableName });
    }

    private static JsonElement MockCreateVariable(MockRuntimeContext context, MicroflowObjectModel obj, MicroflowActionModel action)
    {
        var variableName = ReadString(action.Raw, "variableName") ?? "localVariable";
        var type = action.Raw.TryGetProperty("dataType", out var dataType) ? dataType.Clone() : Type("unknown");
        var rawExpression = ReadExpressionText(action.Raw, "initialValue");
        var evaluated = string.IsNullOrWhiteSpace(rawExpression)
            ? null
            : context.EvaluateExpressionOrThrow(rawExpression!, obj, action, MicroflowExpressionTypeHelper.FromDataType(type, type.GetRawText()));
        var value = evaluated?.Value is not null
            ? MicroflowVariableStore.ToJsonElement(evaluated.Value.RawValueJson) ?? JsonSerializer.SerializeToElement(evaluated.Value.ValuePreview, JsonOptions)
            : JsonSerializer.SerializeToElement((string?)null, JsonOptions);
        context.SetVariable(variableName, type, value, "createVariable", obj.Id, action.Id, obj.CollectionId, readOnly: ReadBool(action.Raw, "readonly"));
        return JsonObj(new { variableName, valuePreview = Preview(value), expressionResult = evaluated });
    }

    private static JsonElement MockChangeVariable(
        MockRuntimeContext context,
        MicroflowObjectModel obj,
        MicroflowActionModel action)
    {
        var variableName = ReadString(action.Raw, "targetVariableName");
        if (string.IsNullOrWhiteSpace(variableName) || !context.Variables.TryGetValue(variableName, out var current))
        {
            return default;
        }

        var type = MicroflowVariableStore.ToJsonElement(current.DataTypeJson) ?? Type("unknown");
        var rawExpression = ReadExpressionText(action.Raw, "newValueExpression");
        var evaluated = context.EvaluateExpressionOrThrow(rawExpression ?? "null", obj, action, MicroflowExpressionTypeHelper.FromDataType(type, type.GetRawText()));
        var value = MicroflowVariableStore.ToJsonElement(evaluated.Value?.RawValueJson) ?? JsonSerializer.SerializeToElement(evaluated.Value?.ValuePreview, JsonOptions);
        context.SetVariable(variableName, type, value, "changeVariable", obj.Id, action.Id, obj.CollectionId);
        return JsonObj(new { variableName, valuePreview = Preview(value), expressionResult = evaluated });
    }

    private static JsonElement MockCallMicroflow(MockRuntimeContext context, MicroflowObjectModel obj, MicroflowActionModel action)
    {
        var storeResult = ReadStringByPath(action.Raw, "returnValue", "outputVariableName") ?? ReadString(action.Raw, "outputVariableName");
        if (!string.IsNullOrWhiteSpace(storeResult))
        {
            context.SetVariable(storeResult, Type("object"), JsonObj(new { mockedReturn = true }), "callMicroflow", obj.Id, action.Id, obj.CollectionId);
        }

        return JsonObj(new { callTarget = ReadString(action.Raw, "targetMicroflowId"), outputVariableName = storeResult });
    }

    private static JsonElement MockRestCall(MockRuntimeContext context, MicroflowObjectModel obj, MicroflowActionModel action)
    {
        var requestPreview = BuildRestRequestPreview(context, obj, action);
        var responseVariable = ReadStringByPath(action.Raw, "response", "handling", "outputVariableName") ?? ReadString(action.Raw, "outputVariableName");
        if (!string.IsNullOrWhiteSpace(responseVariable))
        {
            context.SetVariable(responseVariable, Type("httpResponse"), JsonObj(new { statusCode = 200, body = "mock-rest-response" }), "restCall", obj.Id, action.Id, obj.CollectionId);
        }

        var statusCodeVariable = ReadString(action.Raw, "statusCodeVariableName");
        if (!string.IsNullOrWhiteSpace(statusCodeVariable))
        {
            context.SetVariable(statusCodeVariable, Type("integer"), JsonSerializer.SerializeToElement(200, JsonOptions), "restCall", obj.Id, action.Id, obj.CollectionId);
        }

        var headersVariable = ReadString(action.Raw, "headersVariableName");
        if (!string.IsNullOrWhiteSpace(headersVariable))
        {
            context.SetVariable(headersVariable, Type("json"), JsonObj(new { contentType = "application/json" }), "restCall", obj.Id, action.Id, obj.CollectionId);
        }

        return JsonObj(new { statusCode = 200, outputVariableName = responseVariable, requestPreview });
    }

    private static JsonElement MockLogMessage(MockRuntimeContext context, MicroflowObjectModel obj, MicroflowActionModel action)
    {
        var message = ReadStringByPath(action.Raw, "template", "text") ?? ReadString(action.Raw, "text") ?? obj.Caption ?? "Mock log message";
        var level = ReadString(action.Raw, "level") ?? ReadStringByPath(action.Raw, "template", "level") ?? "info";
        var argumentResults = EvaluateLogArguments(context, obj, action);
        for (var index = 0; index < argumentResults.Count; index++)
        {
            message = message.Replace("{" + index.ToString(CultureInfo.InvariantCulture) + "}", argumentResults[index].ValuePreview, StringComparison.Ordinal);
        }
        if (ReadBool(action.Raw, "includeTraceId"))
        {
            message = $"{message} traceId={context.RunId}";
        }

        context.AddLog(level.ToLowerInvariant(), obj.Id, action.Id, message);
        return JsonObj(new { logged = true, level, message, expressionResults = argumentResults });
    }

    private static JsonElement MockCastObject(MockRuntimeContext context, MicroflowObjectModel obj, MicroflowActionModel action)
    {
        var inputVariableName = ReadString(action.Raw, "inputVariableName") ?? ReadString(action.Raw, "objectVariableName");
        var outputVariableName = ReadString(action.Raw, "outputVariableName") ?? inputVariableName;
        var targetEntity = ReadString(action.Raw, "targetEntityQualifiedName") ?? ReadString(action.Raw, "entityQualifiedName") ?? "Mock.Entity";
        MicroflowRuntimeVariableValue? source = null;
        var sourceExists = !string.IsNullOrWhiteSpace(inputVariableName) && context.Variables.TryGetValue(inputVariableName!, out source);
        var castValue = sourceExists
            ? MicroflowVariableStore.ToJsonElement(source!.RawValueJson) ?? JsonObj(new { id = inputVariableName, entityQualifiedName = targetEntity })
            : JsonSerializer.SerializeToElement((string?)null, JsonOptions);
        if (!string.IsNullOrWhiteSpace(outputVariableName))
        {
            context.SetVariable(outputVariableName!, Type("object", targetEntity), castValue, "cast", obj.Id, action.Id, obj.CollectionId);
        }

        return JsonObj(new { inputVariableName, outputVariableName, targetEntityQualifiedName = targetEntity, castSucceeded = sourceExists });
    }

    private static JsonElement MockCreateList(MockRuntimeContext context, MicroflowObjectModel obj, MicroflowActionModel action)
    {
        var variableName = ReadString(action.Raw, "outputVariableName") ?? ReadString(action.Raw, "listVariableName") ?? "list";
        var itemType = action.Raw.TryGetProperty("itemType", out var configuredItemType)
            ? configuredItemType.Clone()
            : JsonSerializer.SerializeToElement(new { kind = "object", entityQualifiedName = ReadString(action.Raw, "entityQualifiedName") ?? "Mock.Entity" }, JsonOptions);
        var value = JsonObj(new { items = Array.Empty<object>(), count = 0, itemType });
        context.SetVariable(variableName, JsonSerializer.SerializeToElement(new { kind = "list", itemType }, JsonOptions), value, "createList", obj.Id, action.Id, obj.CollectionId);
        return JsonObj(new { outputVariableName = variableName, count = 0, itemType });
    }

    private static JsonElement MockChangeList(MockRuntimeContext context, MicroflowObjectModel obj, MicroflowActionModel action)
    {
        var variableName = ReadString(action.Raw, "listVariableName") ?? ReadString(action.Raw, "targetListVariableName") ?? ReadString(action.Raw, "targetVariableName") ?? "list";
        var operation = ReadString(action.Raw, "operation") ?? ReadString(action.Raw, "changeKind") ?? ReadString(action.Raw, "type") ?? "add";
        var expression = ReadExpressionText(action.Raw, "valueExpression") ?? ReadExpressionText(action.Raw, "value");
        var evaluated = string.IsNullOrWhiteSpace(expression)
            ? null
            : context.EvaluateExpressionOrThrow(expression!, obj, action, expectedType: null);
        var value = JsonObj(new { listVariableName = variableName, operation, valuePreview = evaluated?.ValuePreview, mockedChangeList = true });
        context.SetVariable(variableName, Type("list"), value, "changeList", obj.Id, action.Id, obj.CollectionId);
        return JsonObj(new { listVariableName = variableName, operation, valuePreview = evaluated?.ValuePreview, expressionResult = evaluated });
    }

    private static JsonElement MockListOperation(MockRuntimeContext context, MicroflowObjectModel obj, MicroflowActionModel action)
    {
        var sourceVariableName = ReadString(action.Raw, "sourceListVariableName") ?? ReadString(action.Raw, "listVariableName") ?? ReadString(action.Raw, "inputVariableName");
        var outputVariableName = ReadString(action.Raw, "outputVariableName") ?? sourceVariableName ?? "listResult";
        var operation = ReadString(action.Raw, "operation") ?? ReadString(action.Raw, "operator") ?? ReadString(action.Raw, "listOperation") ?? "count";
        JsonElement value = operation.ToLowerInvariant() switch
        {
            "contains" or "isempty" => JsonSerializer.SerializeToElement(false, JsonOptions),
            "count" => JsonSerializer.SerializeToElement(0, JsonOptions),
            "first" or "last" or "head" => JsonSerializer.SerializeToElement((string?)null, JsonOptions),
            _ => JsonObj(new { items = Array.Empty<object>(), count = 0, operation })
        };
        var type = operation.Equals("contains", StringComparison.OrdinalIgnoreCase) || operation.Equals("isEmpty", StringComparison.OrdinalIgnoreCase)
            ? Type("boolean")
            : operation.Equals("count", StringComparison.OrdinalIgnoreCase)
                ? Type("integer")
                : Type("list");
        context.SetVariable(outputVariableName, type, value, "listOperation", obj.Id, action.Id, obj.CollectionId);
        return JsonObj(new { sourceVariableName, outputVariableName, operation, resultPreview = Preview(value) });
    }

    private static JsonElement MockAggregateList(MockRuntimeContext context, MicroflowObjectModel obj, MicroflowActionModel action)
    {
        var sourceVariableName = ReadString(action.Raw, "sourceListVariableName") ?? ReadString(action.Raw, "listVariableName") ?? ReadString(action.Raw, "inputVariableName");
        var outputVariableName = ReadString(action.Raw, "outputVariableName") ?? "aggregateResult";
        var aggregate = ReadString(action.Raw, "aggregate") ?? ReadString(action.Raw, "operation") ?? "count";
        JsonElement value = aggregate.Equals("any", StringComparison.OrdinalIgnoreCase) || aggregate.Equals("all", StringComparison.OrdinalIgnoreCase)
            ? JsonSerializer.SerializeToElement(false, JsonOptions)
            : JsonSerializer.SerializeToElement(0, JsonOptions);
        var type = aggregate.Equals("any", StringComparison.OrdinalIgnoreCase) || aggregate.Equals("all", StringComparison.OrdinalIgnoreCase)
            ? Type("boolean")
            : Type("decimal");
        context.SetVariable(outputVariableName, type, value, "aggregateList", obj.Id, action.Id, obj.CollectionId);
        return JsonObj(new { sourceVariableName, outputVariableName, aggregate, resultPreview = Preview(value) });
    }

    private static JsonElement MockMetrics(MockRuntimeContext context, MicroflowObjectModel obj, MicroflowActionModel action)
    {
        var name = ReadString(action.Raw, "metricName") ?? ReadString(action.Raw, "name") ?? action.Kind;
        var valueExpression = ReadExpressionText(action.Raw, "valueExpression");
        var evaluated = string.IsNullOrWhiteSpace(valueExpression)
            ? null
            : context.EvaluateExpressionOrThrow(valueExpression!, obj, action, expectedType: null);
        context.AddLog("info", obj.Id, action.Id, $"metrics.{action.Kind}: {name}={evaluated?.ValuePreview ?? "1"}");
        return JsonObj(new { emitted = true, metricKind = action.Kind, metricName = name, valuePreview = evaluated?.ValuePreview ?? "1", expressionResult = evaluated });
    }

    private static JsonElement BuildEndOutput(MockRuntimeContext context, MicroflowObjectModel obj)
    {
        var rawExpression = ReadExpressionText(obj.Raw, "returnValue");
        if (string.IsNullOrWhiteSpace(rawExpression))
        {
            return JsonObj(new { returnValue = (string?)null, mocked = true });
        }

        var expected = obj.Raw.TryGetProperty("returnType", out var returnType)
            ? MicroflowExpressionTypeHelper.FromDataType(returnType, returnType.GetRawText())
            : null;
        var evaluated = context.EvaluateExpressionOrThrow(rawExpression!, obj, action: null, expected);
        return JsonObj(new { returnValue = evaluated.Value?.RawValueJson, valuePreview = evaluated.ValuePreview, expressionResult = evaluated, mocked = true });
    }

    private static DecisionEvaluation SelectDecisionValue(MockRuntimeContext context, MicroflowObjectModel obj)
    {
        if (!string.IsNullOrWhiteSpace(context.Options.EnumerationCaseValue))
        {
            return new DecisionEvaluation(context.Options.EnumerationCaseValue!, null);
        }

        if (context.Options.DecisionBooleanResult is not null)
        {
            return new DecisionEvaluation(context.Options.DecisionBooleanResult.Value ? "true" : "false", null);
        }

        var rawExpression = ReadExpressionTextByPath(obj.Raw, "splitCondition", "expression")
            ?? ReadExpressionTextByPath(obj.Raw, "config", "expression")
            ?? ReadExpressionText(obj.Raw, "expression");
        if (string.IsNullOrWhiteSpace(rawExpression))
        {
            return new DecisionEvaluation("true", null);
        }

        var resultType = ReadStringByPath(obj.Raw, "splitCondition", "resultType") ?? ReadStringByPath(obj.Raw, "config", "resultType");
        var expected = string.Equals(resultType, "enumeration", StringComparison.OrdinalIgnoreCase)
            || string.Equals(resultType, "Enumeration", StringComparison.OrdinalIgnoreCase)
            ? MicroflowExpressionType.Enumeration(ReadStringByPath(obj.Raw, "splitCondition", "enumerationQualifiedName"))
            : MicroflowExpressionType.Simple(MicroflowExpressionTypeKind.Boolean);
        var evaluated = context.EvaluateExpressionOrThrow(rawExpression!, obj, action: null, expected);
        var selected = MicroflowExpressionTypeHelper.IsEnumeration(evaluated.ValueType)
            ? evaluated.Value?.EnumValue ?? evaluated.ValuePreview
            : evaluated.Value?.BoolValue == true ? "true" : "false";
        return new DecisionEvaluation(selected, evaluated);
    }

    private static IReadOnlyList<MicroflowExpressionEvaluationResult> EvaluateLogArguments(
        MockRuntimeContext context,
        MicroflowObjectModel obj,
        MicroflowActionModel action)
    {
        if (!action.Raw.TryGetProperty("template", out var template)
            || !template.TryGetProperty("arguments", out var arguments)
            || arguments.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<MicroflowExpressionEvaluationResult>();
        }

        var results = new List<MicroflowExpressionEvaluationResult>();
        foreach (var argument in arguments.EnumerateArray())
        {
            var raw = ReadExpressionText(argument);
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            results.Add(context.EvaluateExpressionOrThrow(raw!, obj, action, expectedType: null));
        }

        return results;
    }

    private static JsonElement BuildRestRequestPreview(
        MockRuntimeContext context,
        MicroflowObjectModel obj,
        MicroflowActionModel action)
    {
        var method = ReadStringByPath(action.Raw, "request", "method") ?? "GET";
        var urlExpression = ReadExpressionTextByPath(action.Raw, "request", "urlExpression");
        var urlResult = string.IsNullOrWhiteSpace(urlExpression)
            ? null
            : context.EvaluateExpressionOrThrow(urlExpression!, obj, action, MicroflowExpressionType.Simple(MicroflowExpressionTypeKind.String));

        var headers = EvaluateKeyValueExpressions(context, obj, action, "headers");
        var query = EvaluateKeyValueExpressions(context, obj, action, "queryParameters");
        MicroflowExpressionEvaluationResult? body = null;
        if (action.Raw.TryGetProperty("request", out var request)
            && request.TryGetProperty("body", out var bodyConfig)
            && (ReadString(bodyConfig, "kind") is "json" or "text")
            && !string.IsNullOrWhiteSpace(ReadExpressionText(bodyConfig, "expression")))
        {
            body = context.EvaluateExpressionOrThrow(ReadExpressionText(bodyConfig, "expression")!, obj, action, expectedType: null);
        }

        return JsonObj(new
        {
            method,
            url = urlResult?.ValuePreview,
            urlExpressionResult = urlResult,
            headers,
            query,
            bodyPreview = body?.ValuePreview,
            bodyExpressionResult = body,
            externalRequestSent = false
        });
    }

    private static IReadOnlyList<object> EvaluateKeyValueExpressions(
        MockRuntimeContext context,
        MicroflowObjectModel obj,
        MicroflowActionModel action,
        string collectionName)
    {
        if (!action.Raw.TryGetProperty("request", out var request)
            || !request.TryGetProperty(collectionName, out var values)
            || values.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<object>();
        }

        var result = new List<object>();
        foreach (var item in values.EnumerateArray())
        {
            var key = ReadString(item, "key") ?? string.Empty;
            var expression = ReadExpressionText(item, "valueExpression");
            var evaluated = string.IsNullOrWhiteSpace(expression)
                ? null
                : context.EvaluateExpressionOrThrow(expression!, obj, action, MicroflowExpressionType.Simple(MicroflowExpressionTypeKind.String));
            result.Add(new { key, valuePreview = evaluated?.ValuePreview, expressionResult = evaluated });
        }

        return result;
    }

    private static MicroflowRuntimeErrorDto InvalidCase(MicroflowObjectModel obj, string selectedValue)
        => new()
        {
            Code = RuntimeErrorCode.RuntimeInvalidCase,
            Message = $"未找到匹配分支：{selectedValue}",
            ObjectId = obj.Id
        };

    private static MicroflowRuntimeErrorDto UnknownActionError(MicroflowObjectModel obj, string? incomingFlowId)
        => new()
        {
            Code = RuntimeErrorCode.RuntimeUnknownError,
            Message = "Action mock 执行失败。",
            ObjectId = obj.Id,
            ActionId = obj.Action?.Id,
            FlowId = incomingFlowId
        };

    private static JsonElement FrameInput(MicroflowObjectModel obj)
        => JsonObj(new { kind = obj.Kind, actionKind = obj.Action?.Kind });

    private static bool IsKind(MicroflowObjectModel obj, string kind)
        => string.Equals(obj.Kind, kind, StringComparison.OrdinalIgnoreCase);

    private static string? ReadString(JsonElement element, string propertyName)
        => MicroflowSchemaReader.ReadString(element, propertyName);

    private static string? ReadStringByPath(JsonElement element, params string[] path)
        => MicroflowSchemaReader.ReadStringByPath(element, path);

    private static int? ReadInt(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var result)
                ? result
                : null;

    private static string? ReadItemTypeJson(string? dataTypeJson)
    {
        if (string.IsNullOrWhiteSpace(dataTypeJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(dataTypeJson);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("itemType", out var itemType)
                    ? itemType.GetRawText()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadTypeKind(string? dataTypeJson)
    {
        if (string.IsNullOrWhiteSpace(dataTypeJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(dataTypeJson);
            return ReadString(document.RootElement, "kind");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool ReadBool(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.True;

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

    private static string ReadErrorHandling(JsonElement? action)
        => action.HasValue
            ? ReadString(action.Value, "errorHandlingType") ?? ReadStringByPath(action.Value, "errorHandling", "type") ?? "rollback"
            : "rollback";

    private static int CountArray(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Array ? value.GetArrayLength() : 0;

    private static IReadOnlyList<MicroflowRuntimeChangedMember> ReadMemberChanges(JsonElement element)
    {
        if (!element.TryGetProperty("memberChanges", out var changes) || changes.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<MicroflowRuntimeChangedMember>();
        }

        return changes.EnumerateArray()
            .Select((change, index) =>
            {
                var member = ReadString(change, "memberQualifiedName")
                    ?? ReadString(change, "attributeQualifiedName")
                    ?? ReadString(change, "associationQualifiedName")
                    ?? ReadString(change, "memberName")
                    ?? $"member[{index}]";
                var expression = ReadExpressionTextByPath(change, "value", "expression")
                    ?? ReadExpressionText(change, "valueExpression")
                    ?? ReadExpressionText(change, "value")
                    ?? ReadString(change, "valuePreview");
                return new MicroflowRuntimeChangedMember
                {
                    MemberQualifiedName = member,
                    MemberKind = ReadBool(change, "isAssociation") || !string.IsNullOrWhiteSpace(ReadString(change, "associationQualifiedName")) ? "association" : "attribute",
                    AssignmentKind = ReadString(change, "assignmentKind") ?? ReadString(change, "type") ?? "set",
                    AfterValueJson = expression is null ? null : JsonSerializer.Serialize(expression, JsonOptions),
                    ValuePreview = expression is null ? null : MicroflowVariableStore.TrimPreview(expression, 120)
                };
            })
            .ToArray();
    }

    private static string? TryReadObjectIdFromVariable(MockRuntimeContext context, string? variableName)
    {
        if (string.IsNullOrWhiteSpace(variableName)
            || !context.Variables.TryGetValue(variableName!, out var variable)
            || string.IsNullOrWhiteSpace(variable.RawValueJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(variable.RawValueJson);
            return ReadString(document.RootElement, "id");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? TryReadEntityFromVariable(MockRuntimeContext context, string? variableName)
    {
        if (string.IsNullOrWhiteSpace(variableName)
            || !context.Variables.TryGetValue(variableName!, out var variable)
            || string.IsNullOrWhiteSpace(variable.RawValueJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(variable.RawValueJson);
            return ReadString(document.RootElement, "entityQualifiedName");
        }
        catch (JsonException)
        {
            return null;
        }
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
            if (!current.TryGetProperty(part, out current))
            {
                return null;
            }
        }

        return ReadExpressionText(current, path[^1]);
    }

    private static JsonElement InitialValue(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var text = ReadString(element, "raw") ?? ReadString(element, "text") ?? ReadString(element, "expression");
            if (!string.IsNullOrWhiteSpace(text))
            {
                return JsonSerializer.SerializeToElement(text, JsonOptions);
            }
        }

        return element.Clone();
    }

    private static JsonElement Type(string kind, string? entityQualifiedName = null)
        => string.IsNullOrWhiteSpace(entityQualifiedName)
            ? JsonObj(new { kind })
            : JsonObj(new { kind, entityQualifiedName });

    private static JsonElement JsonObj<T>(T value)
        => JsonSerializer.SerializeToElement(value, JsonOptions);

    private static bool ShouldAttachTransaction(string operation)
        => operation is "createObject" or "changeMembers" or "commit" or "delete" or "rollback";

    private static JsonElement MergeOutput<TExtra>(JsonElement output, TExtra extra)
    {
        var merged = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (output.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in output.EnumerateObject())
            {
                merged[property.Name] = property.Value.Clone();
            }
        }
        else
        {
            merged["value"] = output.Clone();
        }

        var extraElement = JsonSerializer.SerializeToElement(extra, JsonOptions);
        foreach (var property in extraElement.EnumerateObject())
        {
            merged[property.Name] = property.Value.Clone();
        }

        return JsonSerializer.SerializeToElement(merged, JsonOptions);
    }

    private static string Preview(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => element.GetRawText(),
            JsonValueKind.Null or JsonValueKind.Undefined => "null",
            _ => element.GetRawText().Length > 80 ? element.GetRawText()[..80] + "..." : element.GetRawText()
        };

    private sealed class MockRuntimeContext
    {
        private readonly IMicroflowClock _clock;
        private readonly string _runId = Guid.NewGuid().ToString("N");
        private readonly MicroflowMockRuntimeRequest _request;
        private readonly RuntimeExecutionContext _runtimeContext;
        private readonly bool _ownsTransaction;
        private int _transactionLogCursor;
        private int _steps;

        public MockRuntimeContext(
            MicroflowMockRuntimeRequest request,
            MicroflowSchemaModel model,
            IMicroflowClock clock,
            IMicroflowExpressionEvaluator expressionEvaluator,
            IMicroflowTransactionManager transactionManager,
            IMicroflowActionExecutorRegistry actionExecutorRegistry,
            IMicroflowRuntimeConnectorRegistry connectorRegistry)
        {
            _request = request;
            Model = model;
            _clock = clock;
            ExpressionEvaluator = expressionEvaluator;
            Options = request.Options;
            Objects = model.Objects.Where(o => !string.IsNullOrWhiteSpace(o.Id)).ToDictionary(o => o.Id, StringComparer.Ordinal);
            Flows = model.Flows.ToArray();
            StartedAt = clock.UtcNow;
            TransactionManager = transactionManager;
            ActionExecutorRegistry = actionExecutorRegistry;
            ConnectorRegistry = connectorRegistry;
            var sharedTransaction = request.ParentRuntimeContext is not null
                && (string.Equals(request.TransactionBoundary, MicroflowCallTransactionBoundary.Inherit, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(request.TransactionBoundary, MicroflowCallTransactionBoundary.SharedTransaction, StringComparison.OrdinalIgnoreCase));
            _ownsTransaction = request.ParentRuntimeContext is null || !sharedTransaction;
            _runtimeContext = RuntimeExecutionContext.Create(
                _runId,
                request.ExecutionPlan ?? BuildFallbackExecutionPlan(request, model),
                MicroflowRuntimeExecutionMode.TestRun,
                request.Input,
                request.RequestContext,
                StartedAt,
                transactionManager,
                new MicroflowRuntimeTransactionOptions
                {
                    Mode = MicroflowRuntimeTransactionMode.SingleRunTransaction,
                    AutoBegin = _ownsTransaction,
                    TraceTransactions = true,
                    RecordBeforeImage = true,
                    RecordAfterImage = true,
                    CreateSavepoints = true
                },
                parentRunId: request.ParentRuntimeContext?.RunId,
                rootRunId: request.ParentRuntimeContext?.RootRunId,
                callCorrelationId: request.ParentRuntimeContext?.CallCorrelationId,
                maxCallDepth: request.MaxCallDepth,
                metadataCatalog: request.Metadata,
                currentCallFrame: request.CallFrame,
                callStackFrames: request.ParentRuntimeContext?.CallStackFrames);
            if (sharedTransaction && request.ParentRuntimeContext is not null)
            {
                _runtimeContext.Transaction = request.ParentRuntimeContext.Transaction;
                _runtimeContext.UnitOfWork = request.ParentRuntimeContext.UnitOfWork;
                _runtimeContext.TransactionOptions = request.ParentRuntimeContext.TransactionOptions;
            }

            if (_ownsTransaction)
            {
                transactionManager.CreateSavepoint(_runtimeContext, "run-start");
            }
            DrainTransactionLogs();
        }

        public MicroflowSchemaModel Model { get; }

        public IReadOnlyDictionary<string, MicroflowObjectModel> Objects { get; }

        public IReadOnlyList<MicroflowFlowModel> Flows { get; }

        public MicroflowTestRunOptionsDto Options { get; }

        public DateTimeOffset StartedAt { get; }

        public string RunId => _runId;

        public IMicroflowExpressionEvaluator ExpressionEvaluator { get; }

        public IMicroflowTransactionManager TransactionManager { get; }

        public IMicroflowActionExecutorRegistry ActionExecutorRegistry { get; }

        public IMicroflowRuntimeConnectorRegistry ConnectorRegistry { get; }

        public RuntimeExecutionContext RuntimeContext => _runtimeContext;

        public MicroflowMetadataCatalogDto? Metadata => _request.Metadata;

        public IReadOnlyDictionary<string, MicroflowRuntimeVariableValue> Variables => _runtimeContext.VariableStore.CurrentVariables;

        public MicroflowExpressionEvaluationResult EvaluateExpressionOrThrow(
            string rawExpression,
            MicroflowObjectModel obj,
            MicroflowActionModel? action,
            MicroflowExpressionType? expectedType)
        {
            var result = ExpressionEvaluator.Evaluate(
                rawExpression,
                new MicroflowExpressionEvaluationContext
                {
                    RuntimeExecutionContext = _runtimeContext,
                    VariableStore = _runtimeContext.VariableStore,
                    MetadataCatalog = Metadata,
                    CurrentObjectId = obj.Id,
                    CurrentActionId = action?.Id,
                    CurrentCollectionId = obj.CollectionId,
                    ExpectedType = expectedType,
                    Mode = MicroflowRuntimeExecutionMode.TestRun,
                    Options = new MicroflowExpressionEvaluationOptions
                    {
                        AllowUnknownVariables = false,
                        AllowUnsupportedFunctions = false,
                        CoerceNumericTypes = true,
                        StrictTypeCheck = true,
                        MaxEvaluationDepth = 64,
                        MaxStringLength = 500
                    }
                });
            if (result.Success)
            {
                return result;
            }

            var error = new MicroflowRuntimeErrorDto
            {
                Code = result.Error?.Code ?? RuntimeErrorCode.RuntimeExpressionError,
                Message = result.Error?.Message ?? "表达式求值失败。",
                ObjectId = obj.Id,
                ActionId = action?.Id,
                Details = JsonSerializer.Serialize(result.Diagnostics, JsonOptions)
            };
            throw new MicroflowExpressionRuntimeFailure(error, JsonObj(new { expression = rawExpression, expressionResult = result }));
        }

        private List<MicroflowTraceFrameDto> Frames { get; } = [];

        private List<MicroflowRuntimeLogDto> Logs { get; } = [];

        private List<MicroflowRunSessionDto> ChildRuns { get; } = [];

        public void SeedInputVariables()
        {
            // RuntimeExecutionContext initializes parameters and system variables from the ExecutionPlan.
        }

        public bool TryStep(out MicroflowRuntimeErrorDto error)
        {
            _steps++;
            var maxSteps = Math.Clamp(Options.MaxSteps ?? 500, 1, 5000);
            if (_steps <= maxSteps)
            {
                error = new MicroflowRuntimeErrorDto();
                return true;
            }

            error = new MicroflowRuntimeErrorDto
            {
                Code = RuntimeErrorCode.RuntimeMaxStepsExceeded,
                Message = $"Mock runner exceeded maxSteps={maxSteps}."
            };
            return false;
        }

        public MicroflowFlowModel? NextNormalFlow(string objectId)
            => Flows.FirstOrDefault(flow => flow.OriginObjectId == objectId && !flow.IsErrorHandler && !IsAnnotationFlow(flow));

        public MicroflowFlowModel? ErrorHandlerFlow(string objectId)
            => Flows.FirstOrDefault(flow => flow.OriginObjectId == objectId && flow.IsErrorHandler);

        public (MicroflowFlowModel? Flow, JsonElement? CaseValue) SelectCaseFlow(string objectId, string selectedValue)
        {
            var candidates = Flows.Where(flow => flow.OriginObjectId == objectId && !flow.IsErrorHandler && !IsAnnotationFlow(flow)).ToArray();
            foreach (var flow in candidates)
            {
                foreach (var caseValue in flow.CaseValues)
                {
                    if (CaseMatches(caseValue, selectedValue))
                    {
                        return (flow, caseValue.Clone());
                    }
                }
            }

            var fallback = candidates.FirstOrDefault(flow => flow.CaseValues.Any(IsFallbackCase));
            if (fallback is not null)
            {
                return (fallback, fallback.CaseValues.First(IsFallbackCase).Clone());
            }

            return (candidates.Length == 1 ? candidates[0] : null, candidates.Length == 1 ? JsonObj(new { value = selectedValue }) : null);
        }

        public MicroflowObjectModel? FirstLoopObject(string loopObjectId)
        {
            var objects = Model.Objects.Where(o => o.ParentLoopObjectId == loopObjectId && !IsKind(o, "annotation") && !IsKind(o, "parameterObject")).ToArray();
            if (objects.Length == 0)
            {
                return null;
            }

            var incomingTargets = Flows
                .Where(flow => flow.InsideLoop && !flow.IsErrorHandler && !IsAnnotationFlow(flow))
                .Select(flow => flow.DestinationObjectId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.Ordinal);
            return objects.FirstOrDefault(o => !incomingTargets.Contains(o.Id)) ?? objects[0];
        }

        public void AddFrame(
            MicroflowObjectModel obj,
            string? incomingFlowId,
            string? outgoingFlowId,
            string status,
            JsonElement? input = null,
            JsonElement? output = null,
            MicroflowRuntimeErrorDto? error = null,
            JsonElement? selectedCaseValue = null,
            JsonElement? loopIteration = null,
            string? message = null)
        {
            var startedAt = _clock.UtcNow;
            var endedAt = _clock.UtcNow;
            Frames.Add(new MicroflowTraceFrameDto
            {
                Id = Guid.NewGuid().ToString("N"),
                RunId = _runId,
                ParentRunId = _runtimeContext.ParentRunId,
                RootRunId = _runtimeContext.RootRunId,
                CallFrameId = _runtimeContext.CurrentCallFrame?.FrameId,
                CallDepth = _runtimeContext.CurrentCallFrame?.Depth,
                CallerObjectId = _runtimeContext.CurrentCallFrame?.CallerObjectId,
                CallerActionId = _runtimeContext.CurrentCallFrame?.CallerActionId,
                ObjectId = obj.Id,
                ActionId = obj.Action?.Id,
                CollectionId = obj.CollectionId,
                IncomingFlowId = incomingFlowId,
                OutgoingFlowId = outgoingFlowId,
                SelectedCaseValue = selectedCaseValue,
                LoopIteration = loopIteration,
                Status = status,
                StartedAt = startedAt,
                EndedAt = endedAt,
                DurationMs = Math.Max(0, (int)(endedAt - startedAt).TotalMilliseconds),
                Input = input,
                Output = output,
                Error = error,
                VariablesSnapshot = SnapshotVariables(obj, Frames.Count + 1),
                Message = message,
                ErrorHandlerVisited = IsErrorHandlerFlow(incomingFlowId) || IsErrorHandlerFlow(outgoingFlowId)
            });
        }

        private bool IsErrorHandlerFlow(string? flowId)
            => !string.IsNullOrWhiteSpace(flowId)
                && Flows.Any(flow => string.Equals(flow.Id, flowId, StringComparison.Ordinal) && flow.IsErrorHandler);

        public void AddLog(string level, string? objectId, string? actionId, string message)
        {
            Logs.Add(new MicroflowRuntimeLogDto
            {
                Id = Guid.NewGuid().ToString("N"),
                Timestamp = _clock.UtcNow,
                Level = NormalizeLevel(level),
                ObjectId = objectId,
                ActionId = actionId,
                Message = message
            });
        }

        public void AddLogs(IEnumerable<MicroflowRuntimeLogDto> logs)
        {
            Logs.AddRange(logs);
        }

        public void AddChildRuns(IEnumerable<MicroflowRunSessionDto> childRuns)
        {
            ChildRuns.AddRange(childRuns);
        }

        public void DrainTransactionLogs()
        {
            var transaction = _runtimeContext.Transaction;
            if (transaction is null || _transactionLogCursor >= transaction.Logs.Count)
            {
                return;
            }

            Logs.AddRange(transaction.Logs.Skip(_transactionLogCursor).Select(log => log.ToRuntimeLogDto()));
            _transactionLogCursor = transaction.Logs.Count;
        }

        public JsonElement WithActionExecutionPreview(
            JsonElement output,
            string operation,
            IMicroflowActionExecutor executor,
            MicroflowActionExecutionResult? result = null)
            => MergeOutput(output, new
            {
                actionKind = operation,
                executorCategory = executor.Category,
                supportLevel = executor.SupportLevel,
                outputPreview = result?.OutputPreview ?? Preview(output),
                producedVariables = result?.ProducedVariables ?? Array.Empty<MicroflowRuntimeVariableValueDto>(),
                runtimeCommands = result?.RuntimeCommands ?? Array.Empty<MicroflowRuntimeCommand>(),
                connectorRequests = result?.ConnectorRequests ?? Array.Empty<MicroflowConnectorExecutionRequest>(),
                transaction = ShouldAttachTransaction(operation)
                    ? _runtimeContext.CreateTransactionSnapshot(operation, maxChangedObjectPreviewCount: 8)
                    : null,
                diagnostics = result?.Diagnostics ?? Array.Empty<MicroflowActionExecutionDiagnostic>(),
                durationMs = result?.DurationMs ?? 0
            });

        public void SetVariable(
            string name,
            JsonElement type,
            JsonElement rawValue,
            string source,
            string? sourceObjectId = null,
            string? sourceActionId = null,
            string? collectionId = null,
            bool readOnly = false,
            bool system = false)
        {
            var rawJson = rawValue.GetRawText();
            var sourceKind = NormalizeSourceKind(source);
            var next = new MicroflowRuntimeVariableValue
            {
                Name = name,
                DataTypeJson = type.GetRawText(),
                Kind = MicroflowVariableStore.InferKind(type.GetRawText(), rawJson),
                RawValueJson = rawJson,
                ValuePreview = Preview(rawValue),
                SourceKind = sourceKind,
                SourceObjectId = sourceObjectId,
                SourceActionId = sourceActionId,
                CollectionId = collectionId,
                Readonly = readOnly,
                System = system,
                ScopeKind = system ? MicroflowVariableScopeKind.System : MicroflowVariableScopeKind.Action,
                CreatedAt = _clock.UtcNow,
                UpdatedAt = _clock.UtcNow
            };

            if (_runtimeContext.VariableStore.Exists(name))
            {
                _runtimeContext.VariableStore.Set(name, next);
                return;
            }

            _runtimeContext.VariableStore.Define(new MicroflowVariableDefinition
            {
                Name = name,
                DataTypeJson = type.GetRawText(),
                Value = next,
                SourceKind = sourceKind,
                SourceObjectId = sourceObjectId,
                SourceActionId = sourceActionId,
                CollectionId = collectionId,
                ScopeKind = next.ScopeKind,
                Readonly = readOnly,
                System = system
            });
        }

        public void SetLatestError(MicroflowRuntimeErrorDto error, MicroflowObjectModel obj)
        {
            if (_runtimeContext.VariableStore.Exists("$latestError"))
            {
                return;
            }

            SetVariable("$latestError", Type("error"), JsonSerializer.SerializeToElement(error, JsonOptions), "errorContext", obj.Id, obj.Action?.Id, obj.CollectionId, readOnly: true);
        }

        public void ClearLoopVariables(string iteratorName)
        {
            _ = iteratorName;
        }

        public IDisposable PushLoopScope(
            string loopObjectId,
            string collectionId,
            string? iteratorVariableName,
            int index,
            JsonElement iteratorValue,
            string? iteratorPreview,
            string? iteratorDataTypeJson = null,
            bool defineIterator = true)
            => _runtimeContext.PushLoopScope(loopObjectId, collectionId, iteratorVariableName, index, iteratorValue, iteratorPreview, iteratorDataTypeJson, defineIterator);

        public IDisposable PushErrorHandlerScope(MicroflowRuntimeErrorDto error, string? errorHandlerFlowId, JsonElement? latestHttpResponse)
            => _runtimeContext.PushErrorHandlerScope(error, errorHandlerFlowId, latestHttpResponse);

        public MicroflowRuntimeErrorDto EndNotReached(MicroflowObjectModel obj, string? flowId)
            => new()
            {
                Code = RuntimeErrorCode.RuntimeEndNotReached,
                Message = $"对象 {obj.Id} 没有可继续执行的 normal outgoing flow。",
                ObjectId = obj.Id,
                ActionId = obj.Action?.Id,
                FlowId = flowId
            };

        public MicroflowRunSessionDto BuildSession(string status, string code, string message)
            => BuildSession(status, new MicroflowRuntimeErrorDto { Code = code, Message = message });

        public MicroflowRunSessionDto BuildSession(string status, MicroflowRuntimeErrorDto? error)
        {
            var endedAt = _clock.UtcNow;
            if (string.Equals(status, "success", StringComparison.OrdinalIgnoreCase)
                && string.Equals(_runtimeContext.Transaction?.Status, MicroflowRuntimeTransactionStatus.Active, StringComparison.OrdinalIgnoreCase))
            {
                if (_ownsTransaction)
                {
                    TransactionManager.Commit(_runtimeContext, "run completed");
                }
            }
            else if (!string.Equals(status, "success", StringComparison.OrdinalIgnoreCase)
                && string.Equals(_runtimeContext.Transaction?.Status, MicroflowRuntimeTransactionStatus.Active, StringComparison.OrdinalIgnoreCase))
            {
                if (_ownsTransaction)
                {
                    TransactionManager.Rollback(_runtimeContext, "run failed", error);
                }
            }

            DrainTransactionLogs();
            var transactionSummary = BuildTransactionSummary();
            var lastOutputFrame = Frames.LastOrDefault(frame => frame.Output.HasValue);
            var output = status == "success" ? lastOutputFrame?.Output : null;
            if (output.HasValue && transactionSummary is not null)
            {
                output = MergeOutput(output.Value, new { transactionSummary });
            }

            return new MicroflowRunSessionDto
            {
                Id = _runId,
                SchemaId = _request.SchemaId,
                ResourceId = _request.ResourceId,
                Version = _request.Version,
                ParentRunId = _runtimeContext.ParentRunId,
                RootRunId = _runtimeContext.RootRunId,
                CallFrameId = _runtimeContext.CurrentCallFrame?.FrameId,
                CallDepth = _runtimeContext.CurrentCallFrame?.Depth,
                StartedAt = StartedAt,
                EndedAt = endedAt,
                Status = status,
                Input = _request.Input,
                Output = output,
                Error = error,
                Trace = Frames,
                Logs = Logs,
                Variables = Frames.Select(frame => new MicroflowVariableSnapshotDto
                {
                    FrameId = frame.Id,
                    ObjectId = frame.ObjectId,
                    Variables = frame.VariablesSnapshot?.Values.ToArray() ?? Array.Empty<MicroflowRuntimeVariableValueDto>()
                }).ToArray(),
                TransactionSummary = transactionSummary,
                ChildRuns = ChildRuns,
                ChildRunIds = ChildRuns.Select(child => child.Id).ToArray()
            };
        }

        private MicroflowRuntimeTransactionSummary? BuildTransactionSummary()
        {
            var transaction = _runtimeContext.Transaction;
            if (transaction is null)
            {
                return null;
            }

            return new MicroflowRuntimeTransactionSummary
            {
                TransactionId = transaction.Id,
                Status = transaction.Status,
                ChangedObjectCount = transaction.ChangedObjects.Count,
                CommittedObjectCount = transaction.CommittedObjects.Count,
                RolledBackObjectCount = transaction.RolledBackObjects.Count,
                LogCount = transaction.Logs.Count,
                DiagnosticsCount = transaction.Diagnostics.Count
            };
        }

        private Dictionary<string, MicroflowRuntimeVariableValueDto> SnapshotVariables(MicroflowObjectModel obj, int stepIndex)
            => _runtimeContext.CreateSnapshot(obj.Id, obj.Action?.Id, obj.CollectionId, stepIndex)
                .ToRuntimeVariableDtos()
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        private static bool IsAnnotationFlow(MicroflowFlowModel flow)
            => string.Equals(flow.Kind, "annotation", StringComparison.OrdinalIgnoreCase)
                || string.Equals(flow.EdgeKind, "annotation", StringComparison.OrdinalIgnoreCase);

        private static bool CaseMatches(JsonElement caseValue, string selectedValue)
        {
            var value = ReadString(caseValue, "value")
                ?? ReadString(caseValue, "persistedValue")
                ?? ReadString(caseValue, "entityQualifiedName")
                ?? ReadString(caseValue, "kind")
                ?? caseValue.GetRawText().Trim('"');
            return string.Equals(value, selectedValue, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsFallbackCase(JsonElement caseValue)
        {
            var value = ReadString(caseValue, "value")
                ?? ReadString(caseValue, "persistedValue")
                ?? ReadString(caseValue, "entityQualifiedName")
                ?? ReadString(caseValue, "kind");
            return value is not null && (value.Equals("fallback", StringComparison.OrdinalIgnoreCase)
                || value.Equals("empty", StringComparison.OrdinalIgnoreCase)
                || value.Equals("noCase", StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizeLevel(string level)
            => level.ToLowerInvariant() switch
            {
                "trace" or "debug" or "info" or "warning" or "error" or "critical" => level.ToLowerInvariant(),
                "warn" => "warning",
                _ => "info"
            };

        private static string NormalizeSourceKind(string source)
            => source switch
            {
                "input" => MicroflowVariableSourceKind.Parameter,
                "retrieve" or "createObject" => MicroflowVariableSourceKind.ActionOutput,
                "createVariable" => MicroflowVariableSourceKind.LocalVariable,
                "changeVariable" => MicroflowVariableSourceKind.LocalVariable,
                "callMicroflow" => MicroflowVariableSourceKind.MicroflowReturn,
                "restCall" => MicroflowVariableSourceKind.RestResponse,
                "errorContext" => MicroflowVariableSourceKind.ErrorContext,
                "loopIterator" => MicroflowVariableSourceKind.LoopIterator,
                "system" => MicroflowVariableSourceKind.System,
                _ => MicroflowVariableSourceKind.Unknown
            };

        private static MicroflowExecutionPlan BuildFallbackExecutionPlan(MicroflowMockRuntimeRequest request, MicroflowSchemaModel model)
            => new()
            {
                Id = Guid.NewGuid().ToString("N"),
                SchemaId = request.SchemaId,
                ResourceId = request.ResourceId,
                Version = request.Version,
                SchemaVersion = model.SchemaVersion,
                StartNodeId = model.Objects.FirstOrDefault(o => IsKind(o, "startEvent"))?.Id ?? string.Empty,
                Parameters = model.Parameters.Select(parameter => new MicroflowExecutionParameter
                {
                    Id = parameter.Id,
                    Name = parameter.Name,
                    DataTypeJson = parameter.Type.Clone(),
                    Required = ReadBool(parameter.Raw, "required"),
                    Documentation = ReadString(parameter.Raw, "documentation") ?? ReadString(parameter.Raw, "description")
                }).ToArray(),
                CreatedAt = DateTimeOffset.UtcNow
            };
    }

    private sealed record ActionOutcome(bool Succeeded, MicroflowRuntimeErrorDto? Error, JsonElement? LatestHttpResponse = null)
    {
        public static ActionOutcome Success() => new(true, null);

        public static ActionOutcome Failed(MicroflowRuntimeErrorDto error, JsonElement? latestHttpResponse = null) => new(false, error, latestHttpResponse);
    }

    private sealed record DecisionEvaluation(string SelectedValue, MicroflowExpressionEvaluationResult? ExpressionResult);

    private sealed class MicroflowExpressionRuntimeFailure : Exception
    {
        public MicroflowExpressionRuntimeFailure(MicroflowRuntimeErrorDto error, JsonElement output)
            : base(error.Message)
        {
            Error = error;
            Output = output;
        }

        public MicroflowRuntimeErrorDto Error { get; }
        public JsonElement Output { get; }
    }

    private enum ExecutionSignalKind
    {
        Success,
        Error,
        Break,
        Continue
    }

    private sealed record ExecutionSignal(ExecutionSignalKind Kind, JsonElement? Output = null, MicroflowRuntimeErrorDto? Error = null)
    {
        public static ExecutionSignal Success(JsonElement? output = null) => new(ExecutionSignalKind.Success, output);

        public static ExecutionSignal Failed(MicroflowRuntimeErrorDto error) => new(ExecutionSignalKind.Error, Error: error);

        public static ExecutionSignal Break() => new(ExecutionSignalKind.Break);

        public static ExecutionSignal Continue() => new(ExecutionSignalKind.Continue);
    }
}
