using System.Globalization;
using System.Text.Json;
using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Services;

namespace Atlas.Application.Microflows.Runtime;

public interface IMicroflowRuntimeEngine
{
    Task<MicroflowRunSessionDto> RunAsync(MicroflowMockRuntimeRequest request, CancellationToken cancellationToken);
}

public sealed class MicroflowRuntimeEngine : IMicroflowRuntimeEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IMicroflowSchemaReader _schemaReader;
    private readonly IMicroflowClock _clock;

    public MicroflowRuntimeEngine(IMicroflowSchemaReader schemaReader, IMicroflowClock clock)
    {
        _schemaReader = schemaReader;
        _clock = clock;
    }

    public Task<MicroflowRunSessionDto> RunAsync(MicroflowMockRuntimeRequest request, CancellationToken cancellationToken)
    {
        var startedAt = _clock.UtcNow;
        var context = new RuntimeContext(request, _schemaReader.Read(request.Schema), startedAt, _clock);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var graph = MicroflowRuntimeGraph.Build(context.Model);
            var bindingError = BindParameters(context);
            if (bindingError is not null)
            {
                return Task.FromResult(context.BuildSession("failed", bindingError));
            }

            var start = graph.FindStart();
            if (!start.Success)
            {
                return Task.FromResult(context.BuildSession("failed", start.Error));
            }

            var currentNodeId = start.Object!.Id;
            string? incomingFlowId = null;
            while (!string.IsNullOrWhiteSpace(currentNodeId))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!context.TryStep(out var stepError))
                {
                    return Task.FromResult(context.BuildSession("failed", stepError));
                }

                if (!graph.Objects.TryGetValue(currentNodeId, out var node))
                {
                    return Task.FromResult(context.BuildSession("failed", Error(RuntimeErrorCode.RuntimeObjectNotFound, $"运行对象不存在：{currentNodeId}", currentNodeId, flowId: incomingFlowId)));
                }

                var execution = ExecuteNode(context, graph, node, incomingFlowId);
                if (!execution.Success)
                {
                    return Task.FromResult(context.BuildSession("failed", execution.Error));
                }

                if (execution.Completed)
                {
                    return Task.FromResult(context.BuildSession("success", null, execution.Output));
                }

                currentNodeId = execution.NextNodeId;
                incomingFlowId = execution.OutgoingFlowId;
            }

            return Task.FromResult(context.BuildSession("failed", Error(RuntimeErrorCode.RuntimeEndNotReached, "微流未到达 End 节点。")));
        }
        catch (OperationCanceledException)
        {
            return Task.FromResult(context.BuildSession("failed", Error(RuntimeErrorCode.RuntimeCancelled, "微流运行已取消。")));
        }
        catch (RuntimeExpressionException ex)
        {
            return Task.FromResult(context.BuildSession("failed", ex.Error));
        }
        catch (JsonException ex)
        {
            return Task.FromResult(context.BuildSession("failed", Error(RuntimeErrorCode.RuntimeUnknownError, "微流 schema 解析失败。", details: ex.Message)));
        }
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

    private static NodeExecution ExecuteNode(RuntimeContext context, MicroflowRuntimeGraph graph, MicroflowObjectModel node, string? incomingFlowId)
        => node.Kind switch
        {
            "startEvent" => ExecuteSingleOutgoing(context, graph, node, incomingFlowId, "Start"),
            "parameterObject" => ExecuteSingleOutgoing(context, graph, node, incomingFlowId, "Parameter"),
            "exclusiveMerge" => ExecuteSingleOutgoing(context, graph, node, incomingFlowId, "Merge"),
            "endEvent" => ExecuteEnd(context, node, incomingFlowId),
            "exclusiveSplit" => ExecuteDecision(context, graph, node, incomingFlowId),
            "actionActivity" => ExecuteAction(context, graph, node, incomingFlowId),
            _ => Unsupported(context, node, incomingFlowId)
        };

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
            output = context.ExpressionEvaluator.Evaluate(expression!, context.Variables);
        }

        context.AddFrame(node, incomingFlowId, null, "success", JsonObj(new { expression }), output, null, "End node reached.");
        return NodeExecution.Done(output);
    }

    private static NodeExecution ExecuteAction(RuntimeContext context, MicroflowRuntimeGraph graph, MicroflowObjectModel node, string? incomingFlowId)
    {
        var action = node.Action;
        if (action is null)
        {
            var error = Error(RuntimeErrorCode.RuntimeUnsupportedAction, $"ActionActivity 缺少 action 配置：{node.Id}", node.Id, flowId: incomingFlowId);
            context.AddNodeFailure(node, incomingFlowId, error);
            return NodeExecution.Failed(error);
        }

        return action.Kind switch
        {
            "createVariable" => ExecuteCreateVariable(context, graph, node, action, incomingFlowId),
            "changeVariable" => ExecuteChangeVariable(context, graph, node, action, incomingFlowId),
            _ => Unsupported(context, node, incomingFlowId, action.Kind)
        };
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
            : context.ExpressionEvaluator.Evaluate(expression!, context.Variables);
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

        var value = context.ExpressionEvaluator.Evaluate(expression!, context.Variables);
        context.SetVariable(variableName!, current.Type, value, "changeVariable");

        return ContinueAfterAction(context, graph, node, incomingFlowId, JsonObj(new { variableName, oldValue = ToPlainValue(current.Value), newValue = ToPlainValue(value) }));
    }

    private static NodeExecution ContinueAfterAction(RuntimeContext context, MicroflowRuntimeGraph graph, MicroflowObjectModel node, string? incomingFlowId, JsonElement output)
    {
        var outgoing = graph.NormalOutgoing(node.Id);
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

        var result = context.ExpressionEvaluator.Evaluate(expression!, context.Variables);
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

    private static MicroflowRuntimeErrorDto Error(string code, string message, string? objectId = null, string? actionId = null, string? flowId = null, string? details = null)
        => new()
        {
            Code = code,
            Message = message,
            ObjectId = objectId,
            ActionId = actionId,
            FlowId = flowId,
            Details = details
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

    private sealed class RuntimeContext
    {
        private readonly MicroflowMockRuntimeRequest _request;
        private readonly IMicroflowClock _clock;
        private int _steps;

        public RuntimeContext(MicroflowMockRuntimeRequest request, MicroflowSchemaModel model, DateTimeOffset startedAt, IMicroflowClock clock)
        {
            _request = request;
            Model = model;
            StartedAt = startedAt;
            _clock = clock;
            ExpressionEvaluator = new MicroflowRuntimeExpressionEvaluator();
        }

        public MicroflowSchemaModel Model { get; }

        public DateTimeOffset StartedAt { get; }

        public IReadOnlyDictionary<string, JsonElement> Input => _request.Input;

        public Dictionary<string, RuntimeVariable> Variables { get; } = new(StringComparer.Ordinal);

        public List<MicroflowTraceFrameDto> Frames { get; } = [];

        public List<MicroflowRuntimeLogDto> Logs { get; } = [];

        public MicroflowRuntimeExpressionEvaluator ExpressionEvaluator { get; }

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
        }

        public void AddNodeFailure(MicroflowObjectModel node, string? incomingFlowId, MicroflowRuntimeErrorDto error)
            => AddFrame(node, incomingFlowId, null, "failed", JsonObj(new { node.Kind, actionKind = node.Action?.Kind }), null, error);

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
            Frames.Add(new MicroflowTraceFrameDto
            {
                Id = Guid.NewGuid().ToString("N"),
                RunId = _request.RequestContext.TraceId ?? string.Empty,
                ObjectId = objectId,
                ActionId = actionId,
                IncomingFlowId = incomingFlowId,
                OutgoingFlowId = outgoingFlowId,
                SelectedCaseValue = selectedCaseValue,
                Status = status,
                StartedAt = started,
                EndedAt = ended,
                DurationMs = Math.Max(0, (int)(ended - started).TotalMilliseconds),
                Input = input,
                Output = output,
                Error = error,
                VariablesSnapshot = SnapshotVariables(),
                Message = message ?? objectTitle
            });
        }

        public MicroflowRunSessionDto BuildSession(string status, MicroflowRuntimeErrorDto? error, JsonElement? output = null)
        {
            var endedAt = _clock.UtcNow;
            var runId = _request.RequestContext.TraceId;
            if (string.IsNullOrWhiteSpace(runId))
            {
                runId = Guid.NewGuid().ToString("N");
            }

            var frames = Frames.Select(frame => frame with { RunId = runId }).ToArray();
            return new MicroflowRunSessionDto
            {
                Id = runId,
                SchemaId = _request.SchemaId,
                ResourceId = _request.ResourceId,
                Version = _request.Version,
                StartedAt = StartedAt,
                EndedAt = endedAt,
                Status = status,
                Input = _request.Input,
                Output = string.Equals(status, "success", StringComparison.OrdinalIgnoreCase) ? output : null,
                Error = error,
                Trace = frames,
                Logs = Logs,
                Variables = frames.Select(frame => new MicroflowVariableSnapshotDto
                {
                    FrameId = frame.Id,
                    ObjectId = frame.ObjectId,
                    Variables = frame.VariablesSnapshot?.Values.ToArray() ?? Array.Empty<MicroflowRuntimeVariableValueDto>()
                }).ToArray()
            };
        }

        private Dictionary<string, MicroflowRuntimeVariableValueDto> SnapshotVariables()
            => Variables.ToDictionary(
                pair => pair.Key,
                pair => new MicroflowRuntimeVariableValueDto
                {
                    Name = pair.Value.Name,
                    Type = pair.Value.Type,
                    ValuePreview = Preview(pair.Value.Value),
                    RawValue = pair.Value.Value,
                    RawValueJson = pair.Value.Value.GetRawText(),
                    Source = pair.Value.Source,
                    ScopeKind = pair.Value.Source == "parameter" ? "parameter" : "local"
                },
                StringComparer.Ordinal);
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
        private MicroflowRuntimeGraph(IReadOnlyDictionary<string, MicroflowObjectModel> objects, IReadOnlyDictionary<string, MicroflowFlowModel> flows)
        {
            Objects = objects;
            Flows = flows;
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

        public StartResult FindStart()
        {
            var starts = Objects.Values.Where(static node => string.Equals(node.Kind, "startEvent", StringComparison.OrdinalIgnoreCase) && !node.InsideLoop).ToArray();
            return starts.Length switch
            {
                1 => StartResult.Ok(starts[0]),
                0 => StartResult.Fail(Error(RuntimeErrorCode.RuntimeStartNotFound, "未找到 Start 节点。")),
                _ => StartResult.Fail(Error(RuntimeErrorCode.RuntimeStartNotFound, $"找到多个 Start 节点：{starts.Length}。"))
            };
        }

        public IReadOnlyList<MicroflowFlowModel> NormalOutgoing(string objectId)
            => Flows.Values
                .Where(flow => string.Equals(flow.OriginObjectId, objectId, StringComparison.Ordinal)
                    && !flow.IsErrorHandler
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

        private static bool CaseMatches(JsonElement caseValue, string expected)
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

    private sealed class MicroflowRuntimeExpressionEvaluator
    {
        public JsonElement Evaluate(string expression, IReadOnlyDictionary<string, RuntimeVariable> variables)
        {
            var parser = new ExpressionParser(expression, variables);
            var value = parser.Parse();
            return JsonSerializer.SerializeToElement(value, JsonOptions);
        }
    }

    private sealed class ExpressionParser
    {
        private readonly IReadOnlyDictionary<string, RuntimeVariable> _variables;
        private readonly IReadOnlyList<Token> _tokens;
        private int _position;

        public ExpressionParser(string expression, IReadOnlyDictionary<string, RuntimeVariable> variables)
        {
            _variables = variables;
            _tokens = Tokenize(expression);
        }

        public object? Parse()
        {
            var value = ParseOr();
            if (Peek().Kind != TokenKind.End)
            {
                throw ExpressionError($"不支持的表达式片段：{Peek().Text}");
            }

            return value;
        }

        private object? ParseOr()
        {
            var left = ParseAnd();
            while (Match(TokenKind.Or))
            {
                left = ToBool(left) || ToBool(ParseAnd());
            }

            return left;
        }

        private object? ParseAnd()
        {
            var left = ParseEquality();
            while (Match(TokenKind.And))
            {
                left = ToBool(left) && ToBool(ParseEquality());
            }

            return left;
        }

        private object? ParseEquality()
        {
            var left = ParseRelational();
            while (true)
            {
                if (Match(TokenKind.Equal))
                {
                    left = Compare(left, ParseRelational()) == 0;
                    continue;
                }

                if (Match(TokenKind.NotEqual))
                {
                    left = Compare(left, ParseRelational()) != 0;
                    continue;
                }

                return left;
            }
        }

        private object? ParseRelational()
        {
            var left = ParseAdditive();
            while (true)
            {
                if (Match(TokenKind.Greater))
                {
                    left = Compare(left, ParseAdditive()) > 0;
                    continue;
                }

                if (Match(TokenKind.GreaterOrEqual))
                {
                    left = Compare(left, ParseAdditive()) >= 0;
                    continue;
                }

                if (Match(TokenKind.Less))
                {
                    left = Compare(left, ParseAdditive()) < 0;
                    continue;
                }

                if (Match(TokenKind.LessOrEqual))
                {
                    left = Compare(left, ParseAdditive()) <= 0;
                    continue;
                }

                return left;
            }
        }

        private object? ParseAdditive()
        {
            var left = ParseUnary();
            while (true)
            {
                if (Match(TokenKind.Plus))
                {
                    var right = ParseUnary();
                    left = left is string || right is string ? $"{left}{right}" : ToDecimal(left) + ToDecimal(right);
                    continue;
                }

                if (Match(TokenKind.Minus))
                {
                    left = ToDecimal(left) - ToDecimal(ParseUnary());
                    continue;
                }

                return left;
            }
        }

        private object? ParseUnary()
        {
            if (Match(TokenKind.Not))
            {
                return !ToBool(ParseUnary());
            }

            if (Match(TokenKind.Minus))
            {
                return -ToDecimal(ParseUnary());
            }

            return ParsePrimary();
        }

        private object? ParsePrimary()
        {
            var token = Peek();
            if (Match(TokenKind.LeftParen))
            {
                var value = ParseOr();
                Expect(TokenKind.RightParen);
                return value;
            }

            if (Match(TokenKind.String))
            {
                return token.Text;
            }

            if (Match(TokenKind.Number))
            {
                return decimal.Parse(token.Text, CultureInfo.InvariantCulture);
            }

            if (Match(TokenKind.True))
            {
                return true;
            }

            if (Match(TokenKind.False))
            {
                return false;
            }

            if (Match(TokenKind.Null))
            {
                return null;
            }

            if (Match(TokenKind.Identifier))
            {
                var name = token.Text.TrimStart('$');
                if (!_variables.TryGetValue(name, out var variable))
                {
                    throw ExpressionError($"变量不存在：{name}");
                }

                return ToPlainValue(variable.Value);
            }

            throw ExpressionError($"不支持的表达式 token：{token.Text}");
        }

        private bool Match(TokenKind kind)
        {
            if (Peek().Kind != kind)
            {
                return false;
            }

            _position++;
            return true;
        }

        private void Expect(TokenKind kind)
        {
            if (!Match(kind))
            {
                throw ExpressionError($"表达式缺少 {kind}。");
            }
        }

        private Token Peek()
            => _tokens[Math.Min(_position, _tokens.Count - 1)];

        private static int Compare(object? left, object? right)
        {
            if (left is null || right is null)
            {
                return left is null && right is null ? 0 : -1;
            }

            if (IsNumber(left) || IsNumber(right))
            {
                return ToDecimal(left).CompareTo(ToDecimal(right));
            }

            if (left is bool lb && right is bool rb)
            {
                return lb.CompareTo(rb);
            }

            return string.Compare(Convert.ToString(left, CultureInfo.InvariantCulture), Convert.ToString(right, CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }

        private static bool ToBool(object? value)
            => value switch
            {
                bool boolean => boolean,
                string text when bool.TryParse(text, out var boolean) => boolean,
                _ => throw ExpressionError("表达式值不能转换为 boolean。")
            };

        private static decimal ToDecimal(object? value)
            => value switch
            {
                decimal number => number,
                int number => number,
                long number => number,
                double number => (decimal)number,
                string text when decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) => number,
                _ => throw ExpressionError("表达式值不能转换为 number。")
            };

        private static bool IsNumber(object value)
            => value is decimal or int or long or double or float;

        private static RuntimeExpressionException ExpressionError(string message)
            => new(Error(RuntimeErrorCode.RuntimeExpressionError, message));

        private static IReadOnlyList<Token> Tokenize(string expression)
        {
            var tokens = new List<Token>();
            var index = 0;
            while (index < expression.Length)
            {
                var ch = expression[index];
                if (char.IsWhiteSpace(ch))
                {
                    index++;
                    continue;
                }

                if (ch is '\'' or '"')
                {
                    var quote = ch;
                    index++;
                    var start = index;
                    while (index < expression.Length && expression[index] != quote)
                    {
                        index++;
                    }

                    if (index >= expression.Length)
                    {
                        throw ExpressionError("字符串字面量未闭合。");
                    }

                    tokens.Add(new Token(TokenKind.String, expression[start..index]));
                    index++;
                    continue;
                }

                if (char.IsDigit(ch))
                {
                    var start = index;
                    while (index < expression.Length && (char.IsDigit(expression[index]) || expression[index] == '.'))
                    {
                        index++;
                    }

                    tokens.Add(new Token(TokenKind.Number, expression[start..index]));
                    continue;
                }

                if (char.IsLetter(ch) || ch == '_' || ch == '$')
                {
                    var start = index;
                    index++;
                    while (index < expression.Length && (char.IsLetterOrDigit(expression[index]) || expression[index] == '_' || expression[index] == '$'))
                    {
                        index++;
                    }

                    var text = expression[start..index];
                    tokens.Add(text switch
                    {
                        "true" => new Token(TokenKind.True, text),
                        "false" => new Token(TokenKind.False, text),
                        "null" => new Token(TokenKind.Null, text),
                        "and" => new Token(TokenKind.And, text),
                        "or" => new Token(TokenKind.Or, text),
                        "not" => new Token(TokenKind.Not, text),
                        _ => new Token(TokenKind.Identifier, text)
                    });
                    continue;
                }

                if (index + 1 < expression.Length)
                {
                    var two = expression[index..(index + 2)];
                    var kind = two switch
                    {
                        "&&" => TokenKind.And,
                        "||" => TokenKind.Or,
                        "==" => TokenKind.Equal,
                        "!=" => TokenKind.NotEqual,
                        ">=" => TokenKind.GreaterOrEqual,
                        "<=" => TokenKind.LessOrEqual,
                        _ => TokenKind.Unknown
                    };
                    if (kind != TokenKind.Unknown)
                    {
                        tokens.Add(new Token(kind, two));
                        index += 2;
                        continue;
                    }
                }

                tokens.Add(ch switch
                {
                    '=' => new Token(TokenKind.Equal, ch.ToString()),
                    '>' => new Token(TokenKind.Greater, ch.ToString()),
                    '<' => new Token(TokenKind.Less, ch.ToString()),
                    '+' => new Token(TokenKind.Plus, ch.ToString()),
                    '-' => new Token(TokenKind.Minus, ch.ToString()),
                    '!' => new Token(TokenKind.Not, ch.ToString()),
                    '(' => new Token(TokenKind.LeftParen, ch.ToString()),
                    ')' => new Token(TokenKind.RightParen, ch.ToString()),
                    _ => throw ExpressionError($"不支持的表达式字符：{ch}")
                });
                index++;
            }

            tokens.Add(new Token(TokenKind.End, string.Empty));
            return tokens;
        }
    }

    private sealed record Token(TokenKind Kind, string Text);

    private enum TokenKind
    {
        Unknown,
        End,
        Identifier,
        String,
        Number,
        True,
        False,
        Null,
        And,
        Or,
        Not,
        Equal,
        NotEqual,
        Greater,
        GreaterOrEqual,
        Less,
        LessOrEqual,
        Plus,
        Minus,
        LeftParen,
        RightParen
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
