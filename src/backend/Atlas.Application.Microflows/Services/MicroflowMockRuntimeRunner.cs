using System.Text.Json;
using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime;

namespace Atlas.Application.Microflows.Services;

public sealed class MicroflowMockRuntimeRunner : IMicroflowMockRuntimeRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> SupportedActionKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "retrieve", "createObject", "changeMembers", "commit", "delete", "rollback", "createVariable", "changeVariable",
        "callMicroflow", "restCall", "logMessage"
    };

    private readonly IMicroflowSchemaReader _schemaReader;
    private readonly IMicroflowClock _clock;

    public MicroflowMockRuntimeRunner(IMicroflowSchemaReader schemaReader, IMicroflowClock clock)
    {
        _schemaReader = schemaReader;
        _clock = clock;
    }

    public Task<MicroflowRunSessionDto> RunAsync(MicroflowMockRuntimeRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var context = new MockRuntimeContext(request, _schemaReader.Read(request.Schema), _clock);
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
                var output = BuildEndOutput(obj);
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
                context.AddFrame(obj, incoming, null, "success", input: FrameInput(obj), output: JsonObj(new { signal = "break" }), loopIteration: loopIteration);
                return ExecutionSignal.Break();
            }

            if (IsKind(obj, "continueEvent"))
            {
                context.AddFrame(obj, incoming, null, "success", input: FrameInput(obj), output: JsonObj(new { signal = "continue" }), loopIteration: loopIteration);
                return ExecutionSignal.Continue();
            }

            if (IsKind(obj, "exclusiveSplit"))
            {
                var selectedValue = SelectDecisionValue(context.Options);
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
                var outcome = ExecuteAction(context, obj, incoming, loopIteration);
                if (!outcome.Succeeded)
                {
                    var actionError = outcome.Error ?? UnknownActionError(obj, incoming);
                    var handling = ReadErrorHandling(obj.Action?.Raw);
                    var errorFlow = context.ErrorHandlerFlow(obj.Id);
                    if (string.Equals(handling, "continue", StringComparison.OrdinalIgnoreCase))
                    {
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
                            context.AddLog("warning", obj.Id, obj.Action?.Id, "Mock transaction rolled back before custom error handler.");
                        }

                        using var errorScope = context.PushErrorHandlerScope(actionError, errorFlow.Id, outcome.LatestHttpResponse);
                        var handled = ExecutePath(context, errorFlow.DestinationObjectId!, errorFlow.Id, loopIteration, cancellationToken);
                        return handled.Kind == ExecutionSignalKind.Error ? handled : ExecutionSignal.Success(handled.Output);
                    }

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
        var iterations = Math.Clamp(context.Options.LoopIterations ?? 2, 0, 50);
        var iteratorName = ReadStringByPath(loop.Raw, "loopSource", "iteratorVariableName") ?? "$iterator";
        context.AddFrame(
            loop,
            incomingFlowId,
            context.NextNormalFlow(loop.Id)?.Id,
            "success",
            input: FrameInput(loop),
            output: JsonObj(new { iterations }),
            loopIteration: outerLoopIteration,
            message: $"Mock loop iterations: {iterations}.");

        var start = context.FirstLoopObject(loop.Id);
        if (start is null || iterations == 0)
        {
            return ExecutionSignal.Success();
        }

        for (var index = 0; index < iterations; index++)
        {
            var iteratorPreview = $"{iteratorName}[{index}]";
            var iteratorValue = JsonObj(new { id = $"{loop.Id}-item-{index}", preview = iteratorPreview });
            var iterationJson = JsonObj(new
            {
                loopObjectId = loop.Id,
                index,
                iteratorVariableName = iteratorName,
                iteratorValuePreview = iteratorPreview
            });
            using (context.PushLoopScope(loop.Id, loop.CollectionId, iteratorName, index, iteratorValue, iteratorPreview))
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

    private static ActionOutcome ExecuteAction(
        MockRuntimeContext context,
        MicroflowObjectModel obj,
        string? incomingFlowId,
        JsonElement? loopIteration)
    {
        var action = obj.Action;
        if (action is null || string.IsNullOrWhiteSpace(action.Kind) || !SupportedActionKinds.Contains(action.Kind) || ReadBool(action.Raw, "modeledOnly"))
        {
            var error = new MicroflowRuntimeErrorDto
            {
                Code = RuntimeErrorCode.RuntimeUnsupportedAction,
                Message = $"Action 类型暂不支持：{action?.Kind ?? "missing"}",
                ObjectId = obj.Id,
                ActionId = action?.Id,
                FlowId = incomingFlowId
            };
            context.AddFrame(obj, incomingFlowId, null, "failed", input: FrameInput(obj), error: error, loopIteration: loopIteration);
            return ActionOutcome.Failed(error);
        }

        if (string.Equals(action.Kind, "restCall", StringComparison.OrdinalIgnoreCase) && context.Options.SimulateRestError == true)
        {
            var error = new MicroflowRuntimeErrorDto
            {
                Code = RuntimeErrorCode.RuntimeRestCallFailed,
                Message = "Mock REST call failed by simulateRestError.",
                ObjectId = obj.Id,
                ActionId = action.Id,
                FlowId = incomingFlowId,
                Details = "No external HTTP request was sent."
            };
            var latestHttpResponse = JsonObj(new { statusCode = 500, body = "mock-rest-error", headers = new { contentType = "application/json" } });
            context.AddFrame(obj, incomingFlowId, context.ErrorHandlerFlow(obj.Id)?.Id, "failed", input: FrameInput(obj), error: error, loopIteration: loopIteration, message: "REST error path mocked.");
            return ActionOutcome.Failed(error, latestHttpResponse);
        }

        JsonElement output;
        try
        {
            output = action.Kind switch
            {
                "retrieve" => MockRetrieve(context, obj, action),
                "createObject" => MockCreateObject(context, obj, action),
                "changeMembers" => MockChangeMembers(context, action),
                "commit" => JsonObj(new { committed = true, variableName = ReadString(action.Raw, "objectOrListVariableName") }),
                "delete" => JsonObj(new { deleted = true, variableName = ReadString(action.Raw, "objectOrListVariableName") }),
                "rollback" => JsonObj(new { rolledBack = true, variableName = ReadString(action.Raw, "objectOrListVariableName") }),
                "createVariable" => MockCreateVariable(context, obj, action),
                "changeVariable" => MockChangeVariable(context, obj, action),
                "callMicroflow" => MockCallMicroflow(context, obj, action),
                "restCall" => MockRestCall(context, obj, action),
                "logMessage" => MockLogMessage(context, obj, action),
                _ => JsonObj(new { mocked = true })
            };
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

        context.AddFrame(obj, incomingFlowId, context.NextNormalFlow(obj.Id)?.Id, "success", input: FrameInput(obj), output: output, loopIteration: loopIteration);
        return ActionOutcome.Success();
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
        var changes = CountArray(action.Raw, "memberChanges");
        var value = JsonObj(new { id = Guid.NewGuid().ToString("N"), entityQualifiedName = entity, changedMembers = changes });
        context.SetVariable(variableName, Type("object"), value, "createObject", obj.Id, action.Id, obj.CollectionId);
        return JsonObj(new { outputVariableName = variableName, entityQualifiedName = entity, changedMembers = changes });
    }

    private static JsonElement MockChangeMembers(MockRuntimeContext context, MicroflowActionModel action)
        => JsonObj(new { variableName = ReadString(action.Raw, "changeVariableName"), changedMembers = CountArray(action.Raw, "memberChanges") });

    private static JsonElement MockCreateVariable(MockRuntimeContext context, MicroflowObjectModel obj, MicroflowActionModel action)
    {
        var variableName = ReadString(action.Raw, "variableName") ?? "localVariable";
        var value = action.Raw.TryGetProperty("initialValue", out var initial) ? InitialValue(initial) : JsonSerializer.SerializeToElement("mock", JsonOptions);
        var type = action.Raw.TryGetProperty("dataType", out var dataType) ? dataType.Clone() : Type("unknown");
        context.SetVariable(variableName, type, value, "createVariable", obj.Id, action.Id, obj.CollectionId, readOnly: ReadBool(action.Raw, "readonly"));
        return JsonObj(new { variableName, valuePreview = Preview(value) });
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

        var value = action.Raw.TryGetProperty("newValueExpression", out var expression) ? InitialValue(expression) : JsonSerializer.SerializeToElement("changed", JsonOptions);
        context.SetVariable(variableName, MicroflowVariableStore.ToJsonElement(current.DataTypeJson) ?? Type("unknown"), value, "changeVariable", obj.Id, action.Id, obj.CollectionId);
        return JsonObj(new { variableName, valuePreview = Preview(value) });
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

        return JsonObj(new { statusCode = 200, outputVariableName = responseVariable });
    }

    private static JsonElement MockLogMessage(MockRuntimeContext context, MicroflowObjectModel obj, MicroflowActionModel action)
    {
        var message = ReadStringByPath(action.Raw, "template", "text") ?? ReadString(action.Raw, "text") ?? obj.Caption ?? "Mock log message";
        var level = ReadString(action.Raw, "level") ?? ReadStringByPath(action.Raw, "template", "level") ?? "info";
        context.AddLog(level.ToLowerInvariant(), obj.Id, action.Id, message);
        return JsonObj(new { logged = true, level, message });
    }

    private static JsonElement BuildEndOutput(MicroflowObjectModel obj)
        => obj.Raw.TryGetProperty("returnValue", out var returnValue) && returnValue.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
            ? JsonObj(new { returnValue = returnValue.Clone(), mocked = true })
            : JsonObj(new { returnValue = (string?)null, mocked = true });

    private static string SelectDecisionValue(MicroflowTestRunOptionsDto options)
        => options.EnumerationCaseValue ?? ((options.DecisionBooleanResult ?? true) ? "true" : "false");

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

    private static bool ReadBool(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.True;

    private static string ReadErrorHandling(JsonElement? action)
        => action.HasValue
            ? ReadString(action.Value, "errorHandlingType") ?? ReadStringByPath(action.Value, "errorHandling", "type") ?? "rollback"
            : "rollback";

    private static int CountArray(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Array ? value.GetArrayLength() : 0;

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

    private static JsonElement Type(string kind)
        => JsonObj(new { kind });

    private static JsonElement JsonObj<T>(T value)
        => JsonSerializer.SerializeToElement(value, JsonOptions);

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
        private int _steps;

        public MockRuntimeContext(MicroflowMockRuntimeRequest request, MicroflowSchemaModel model, IMicroflowClock clock)
        {
            _request = request;
            Model = model;
            _clock = clock;
            Options = request.Options;
            Objects = model.Objects.Where(o => !string.IsNullOrWhiteSpace(o.Id)).ToDictionary(o => o.Id, StringComparer.Ordinal);
            Flows = model.Flows.ToArray();
            StartedAt = clock.UtcNow;
            _runtimeContext = RuntimeExecutionContext.Create(
                _runId,
                request.ExecutionPlan ?? BuildFallbackExecutionPlan(request, model),
                MicroflowRuntimeExecutionMode.TestRun,
                request.Input,
                request.RequestContext,
                StartedAt);
        }

        public MicroflowSchemaModel Model { get; }

        public IReadOnlyDictionary<string, MicroflowObjectModel> Objects { get; }

        public IReadOnlyList<MicroflowFlowModel> Flows { get; }

        public MicroflowTestRunOptionsDto Options { get; }

        public DateTimeOffset StartedAt { get; }

        public IReadOnlyDictionary<string, MicroflowRuntimeVariableValue> Variables => _runtimeContext.VariableStore.CurrentVariables;

        private List<MicroflowTraceFrameDto> Frames { get; } = [];

        private List<MicroflowRuntimeLogDto> Logs { get; } = [];

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
            string iteratorVariableName,
            int index,
            JsonElement iteratorValue,
            string iteratorPreview)
            => _runtimeContext.PushLoopScope(loopObjectId, collectionId, iteratorVariableName, index, iteratorValue, iteratorPreview);

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
            var lastOutputFrame = Frames.LastOrDefault(frame => frame.Output.HasValue);
            return new MicroflowRunSessionDto
            {
                Id = _runId,
                SchemaId = _request.SchemaId,
                ResourceId = _request.ResourceId,
                Version = _request.Version,
                StartedAt = StartedAt,
                EndedAt = endedAt,
                Status = status,
                Input = _request.Input,
                Output = status == "success" ? lastOutputFrame?.Output : null,
                Error = error,
                Trace = Frames,
                Logs = Logs,
                Variables = Frames.Select(frame => new MicroflowVariableSnapshotDto
                {
                    FrameId = frame.Id,
                    ObjectId = frame.ObjectId,
                    Variables = frame.VariablesSnapshot?.Values.ToArray() ?? Array.Empty<MicroflowRuntimeVariableValueDto>()
                }).ToArray()
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
