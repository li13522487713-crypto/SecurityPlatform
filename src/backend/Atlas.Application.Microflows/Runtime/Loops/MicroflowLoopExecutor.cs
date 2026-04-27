using System.Text.Json;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime.Actions;
using Atlas.Application.Microflows.Runtime.Expressions;
using Atlas.Application.Microflows.Runtime.Transactions;
using Atlas.Application.Microflows.Services;

namespace Atlas.Application.Microflows.Runtime.Loops;

public sealed class MicroflowLoopExecutor : IMicroflowLoopExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IMicroflowExpressionEvaluator _expressionEvaluator;
    private readonly IMicroflowActionExecutorRegistry _actionExecutorRegistry;
    private readonly IMicroflowTransactionManager _transactionManager;

    public MicroflowLoopExecutor(
        IMicroflowExpressionEvaluator expressionEvaluator,
        IMicroflowActionExecutorRegistry actionExecutorRegistry,
        IMicroflowTransactionManager transactionManager)
    {
        _expressionEvaluator = expressionEvaluator;
        _actionExecutorRegistry = actionExecutorRegistry;
        _transactionManager = transactionManager;
    }

    public async Task<MicroflowLoopExecutionResult> ExecuteLoopAsync(
        MicroflowActionExecutionContext context,
        MicroflowExecutionNode loopNode,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (context.LoopBodyExecutor is null)
        {
            return Failed(RuntimeErrorCode.RuntimeLoopBodyNotFound, "Loop body executor is not available.", loopNode);
        }

        var query = new MicroflowExecutionPlanQuery(context.ExecutionPlan);
        var loopCollection = query.GetLoopCollection(context.ExecutionPlan, loopNode.ObjectId);
        if (loopCollection is null)
        {
            return Failed(RuntimeErrorCode.RuntimeLoopBodyNotFound, "Loop collection is missing.", loopNode);
        }

        var entryNodeId = query.FindLoopEntryNodeId(context.ExecutionPlan, loopCollection);
        if (string.IsNullOrWhiteSpace(entryNodeId))
        {
            return Failed(RuntimeErrorCode.RuntimeLoopBodyNotFound, "Loop body entry node is missing.", loopNode);
        }

        var options = NormalizeOptions(context.LoopExecutionOptions, loopNode);
        var loopContext = new MicroflowLoopExecutionContext
        {
            RuntimeExecutionContext = context.RuntimeExecutionContext,
            ExecutionPlan = context.ExecutionPlan,
            LoopNode = loopNode,
            LoopCollection = loopCollection,
            VariableStore = context.VariableStore,
            ExpressionEvaluator = context.ExpressionEvaluator ?? _expressionEvaluator,
            ActionExecutorRegistry = _actionExecutorRegistry,
            TransactionManager = context.TransactionManager ?? _transactionManager,
            Options = options,
            LoopStack = context.RuntimeExecutionContext.LoopStack.Reverse().ToArray(),
            Diagnostics = loopNode.Diagnostics,
            CancellationToken = ct
        };

        var source = ReadLoopSource(loopNode);
        var kind = ReadString(source, "kind") ?? InferLoopKind(source);
        return string.Equals(kind, "whileCondition", StringComparison.OrdinalIgnoreCase)
            ? await ExecuteWhileAsync(context, loopContext, source, ct).ConfigureAwait(false)
            : await ExecuteIterableAsync(context, loopContext, source, ct).ConfigureAwait(false);
    }

    private static async Task<MicroflowLoopExecutionResult> ExecuteIterableAsync(
        MicroflowActionExecutionContext actionContext,
        MicroflowLoopExecutionContext loopContext,
        JsonElement source,
        CancellationToken ct)
    {
        var loopNode = loopContext.LoopNode;
        var listVariableName = ReadString(source, "listVariableName");
        if (string.IsNullOrWhiteSpace(listVariableName))
        {
            return Failed(RuntimeErrorCode.RuntimeLoopSourceNotFound, "Iterable list loop source listVariableName is missing.", loopNode);
        }

        if (!loopContext.VariableStore.TryGet(listVariableName!, out var listVariable) || listVariable is null)
        {
            return Failed(RuntimeErrorCode.RuntimeVariableNotFound, $"Loop list variable '{listVariableName}' was not found.", loopNode);
        }

        if (!TryGetListItems(listVariable, out var items, out var itemTypeJson))
        {
            return Failed(RuntimeErrorCode.RuntimeLoopSourceNotList, $"Loop source '{listVariableName}' is not a list.", loopNode);
        }

        var iteratorName = NormalizeIteratorName(ReadString(source, "iteratorVariableName"));
        if (string.IsNullOrWhiteSpace(iteratorName))
        {
            return Failed(RuntimeErrorCode.RuntimeLoopIteratorInvalid, "Iterable list loop iteratorVariableName is missing.", loopNode);
        }

        if (items.Count == 0)
        {
            return Success(0, loopContext, JsonSerializer.SerializeToElement(new
            {
                mode = "iterableList",
                listVariableName,
                itemCount = 0,
                iterations = 0
            }, JsonOptions));
        }

        var iterationCount = 0;
        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            if (iterationCount >= loopContext.Options.MaxIterations)
            {
                return MaxIterationsExceeded(loopContext, iterationCount);
            }

            var preview = MicroflowVariableStore.Preview(item.GetRawText());
            var iteration = CreateIteration(loopContext, iterationCount, iteratorName, preview, conditionResult: null, itemCount: items.Count, item, itemTypeJson);
            using (loopContext.RuntimeExecutionContext.PushLoopScope(
                loopContext.LoopCollection.LoopObjectId,
                loopContext.LoopCollection.CollectionId,
                iteratorName,
                iterationCount,
                item,
                preview,
                itemTypeJson,
                defineIterator: true))
            {
                var body = await actionContext.LoopBodyExecutor!(iteration, ct).ConfigureAwait(false);
                iterationCount++;
                if (body.Status == MicroflowLoopBodyExecutionStatus.Break)
                {
                    return Control(MicroflowLoopExecutionStatus.Break, iterationCount, loopContext, MicroflowLoopControlSignal.Break);
                }

                if (body.Status == MicroflowLoopBodyExecutionStatus.Continue)
                {
                    continue;
                }

                if (body.Status == MicroflowLoopBodyExecutionStatus.Cancelled)
                {
                    return Failed(RuntimeErrorCode.RuntimeCancelled, "Loop execution was cancelled.", loopContext.LoopNode, MicroflowLoopExecutionStatus.Cancelled);
                }

                if (body.Status == MicroflowLoopBodyExecutionStatus.MaxStepsExceeded)
                {
                    return Failed(RuntimeErrorCode.RuntimeMaxStepsExceeded, "Loop body exceeded maxSteps.", loopContext.LoopNode, MicroflowLoopExecutionStatus.Failed, body.Error);
                }

                if (body.Status == MicroflowLoopBodyExecutionStatus.Failed)
                {
                    return Failed(body.Error ?? Error(RuntimeErrorCode.RuntimeLoopDeadEnd, "Loop body failed.", loopContext.LoopNode));
                }
            }
        }

        return Success(iterationCount, loopContext, JsonSerializer.SerializeToElement(new
        {
            mode = "iterableList",
            listVariableName,
            iteratorVariableName = iteratorName,
            itemCount = items.Count,
            iterations = iterationCount
        }, JsonOptions));
    }

    private static async Task<MicroflowLoopExecutionResult> ExecuteWhileAsync(
        MicroflowActionExecutionContext actionContext,
        MicroflowLoopExecutionContext loopContext,
        JsonElement source,
        CancellationToken ct)
    {
        var expression = ReadExpressionText(source, "expression");
        if (string.IsNullOrWhiteSpace(expression))
        {
            return Failed(RuntimeErrorCode.RuntimeLoopConditionError, "While loop expression is missing.", loopContext.LoopNode);
        }

        var iteratorName = NormalizeIteratorName(ReadString(source, "iteratorVariableName"));
        var iterationCount = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            if (iterationCount >= loopContext.Options.MaxIterations)
            {
                return MaxIterationsExceeded(loopContext, iterationCount);
            }

            var condition = loopContext.ExpressionEvaluator.Evaluate(
                expression!,
                new MicroflowExpressionEvaluationContext
                {
                    RuntimeExecutionContext = loopContext.RuntimeExecutionContext,
                    VariableStore = loopContext.VariableStore,
                    CurrentObjectId = loopContext.LoopNode.ObjectId,
                    CurrentActionId = loopContext.LoopNode.ActionId,
                    CurrentCollectionId = loopContext.LoopNode.CollectionId,
                    ExpectedType = MicroflowExpressionType.Simple(MicroflowExpressionTypeKind.Boolean),
                    Mode = loopContext.RuntimeExecutionContext.Mode,
                    Options = new MicroflowExpressionEvaluationOptions
                    {
                        AllowUnknownVariables = false,
                        AllowUnsupportedFunctions = false,
                        StrictTypeCheck = true
                    }
                });
            if (!condition.Success)
            {
                return Failed(RuntimeErrorCode.RuntimeLoopConditionError, condition.Error?.Message ?? "While loop condition evaluation failed.", loopContext.LoopNode, details: JsonSerializer.Serialize(condition.Diagnostics, JsonOptions));
            }

            if (condition.Value?.BoolValue is null)
            {
                return Failed(RuntimeErrorCode.RuntimeLoopConditionNotBoolean, "While loop condition did not evaluate to boolean.", loopContext.LoopNode);
            }

            if (condition.Value.BoolValue != true)
            {
                return Success(iterationCount, loopContext, JsonSerializer.SerializeToElement(new
                {
                    mode = "whileCondition",
                    conditionResult = false,
                    iterations = iterationCount
                }, JsonOptions));
            }

            var iteration = CreateIteration(loopContext, iterationCount, iteratorName, iteratorName is null ? null : $"{iteratorName}[{iterationCount}]", conditionResult: true, itemCount: null, iteratorRawValue: null, iteratorDataTypeJson: null);
            using (loopContext.RuntimeExecutionContext.PushLoopScope(
                loopContext.LoopCollection.LoopObjectId,
                loopContext.LoopCollection.CollectionId,
                iteratorName,
                iterationCount,
                iteratorRawValue: null,
                iteratorPreview: iteratorName is null ? null : $"{iteratorName}[{iterationCount}]",
                iteratorDataTypeJson: null,
                defineIterator: iteratorName is not null))
            {
                var body = await actionContext.LoopBodyExecutor!(iteration, ct).ConfigureAwait(false);
                iterationCount++;
                if (body.Status == MicroflowLoopBodyExecutionStatus.Break)
                {
                    return Control(MicroflowLoopExecutionStatus.Break, iterationCount, loopContext, MicroflowLoopControlSignal.Break);
                }

                if (body.Status == MicroflowLoopBodyExecutionStatus.Continue)
                {
                    continue;
                }

                if (body.Status == MicroflowLoopBodyExecutionStatus.Cancelled)
                {
                    return Failed(RuntimeErrorCode.RuntimeCancelled, "Loop execution was cancelled.", loopContext.LoopNode, MicroflowLoopExecutionStatus.Cancelled);
                }

                if (body.Status == MicroflowLoopBodyExecutionStatus.MaxStepsExceeded)
                {
                    return Failed(RuntimeErrorCode.RuntimeMaxStepsExceeded, "Loop body exceeded maxSteps.", loopContext.LoopNode, MicroflowLoopExecutionStatus.Failed, body.Error);
                }

                if (body.Status == MicroflowLoopBodyExecutionStatus.Failed)
                {
                    return Failed(body.Error ?? Error(RuntimeErrorCode.RuntimeLoopDeadEnd, "Loop body failed.", loopContext.LoopNode));
                }
            }
        }
    }

    private static MicroflowLoopIterationContext CreateIteration(
        MicroflowLoopExecutionContext context,
        int index,
        string? iteratorName,
        string? iteratorPreview,
        bool? conditionResult,
        int? itemCount,
        JsonElement? iteratorRawValue,
        string? iteratorDataTypeJson)
    {
        var iteration = new MicroflowLoopIterationContext
        {
            LoopObjectId = context.LoopCollection.LoopObjectId,
            CollectionId = context.LoopCollection.CollectionId,
            Index = index,
            IteratorVariableName = iteratorName,
            IteratorValuePreview = iteratorPreview,
            ParentLoopObjectId = context.LoopNode.ParentLoopObjectId,
            Depth = context.RuntimeExecutionContext.LoopStack.Count + 1,
            ConditionResult = conditionResult,
            ItemCount = itemCount,
            IteratorRawValue = iteratorRawValue,
            IteratorDataTypeJson = iteratorDataTypeJson
        };
        return iteration with { LoopIterationJson = MicroflowLoopIterationJson.Create(iteration) };
    }

    private static MicroflowLoopExecutionOptions NormalizeOptions(MicroflowLoopExecutionOptions? options, MicroflowExecutionNode loopNode)
    {
        var configuredMax = ReadInt(ReadLoopSource(loopNode), "maxIterations");
        var effective = options ?? new MicroflowLoopExecutionOptions();
        var maxIterations = configuredMax is > 0 ? configuredMax.Value : effective.MaxIterations;
        return effective with
        {
            MaxIterations = maxIterations <= 0 ? 100 : maxIterations
        };
    }

    private static JsonElement ReadLoopSource(MicroflowExecutionNode loopNode)
    {
        if (loopNode.ConfigJson is not { ValueKind: JsonValueKind.Object } config)
        {
            return default;
        }

        if (config.TryGetProperty("raw", out var raw)
            && raw.ValueKind == JsonValueKind.Object
            && raw.TryGetProperty("loopSource", out var rawSource)
            && rawSource.ValueKind == JsonValueKind.Object)
        {
            return rawSource;
        }

        return config.TryGetProperty("loopSource", out var source) && source.ValueKind == JsonValueKind.Object
            ? source
            : default;
    }

    private static string InferLoopKind(JsonElement source)
        => !string.IsNullOrWhiteSpace(ReadExpressionText(source, "expression"))
            ? "whileCondition"
            : "iterableList";

    private static bool TryGetListItems(MicroflowRuntimeVariableValue variable, out IReadOnlyList<JsonElement> items, out string? itemTypeJson)
    {
        itemTypeJson = ReadItemTypeJson(variable.DataTypeJson);
        items = Array.Empty<JsonElement>();
        if (!string.Equals(variable.Kind, MicroflowRuntimeVariableKind.List, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(ReadKind(variable.DataTypeJson), "list", StringComparison.OrdinalIgnoreCase))
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
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                items = root.EnumerateArray().Select(item => item.Clone()).ToArray();
                return true;
            }

            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("items", out var objectItems)
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

    private static string? ReadKind(string? dataTypeJson)
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

    private static string? NormalizeIteratorName(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value!.Trim();

    private static string? ReadString(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

    private static int? ReadInt(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var result)
                ? result
                : null;

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

    private static MicroflowLoopExecutionResult Success(int iterationCount, MicroflowLoopExecutionContext context, JsonElement output)
        => new()
        {
            Status = MicroflowLoopExecutionStatus.Success,
            IterationCount = iterationCount,
            TransactionSnapshot = context.Options.IncludeTransactionSnapshot ? context.RuntimeExecutionContext.CreateTransactionSnapshot("loop") : null,
            OutputPreview = output
        };

    private static MicroflowLoopExecutionResult Control(string status, int iterationCount, MicroflowLoopExecutionContext context, string controlSignal)
        => new()
        {
            Status = status,
            IterationCount = iterationCount,
            TransactionSnapshot = context.Options.IncludeTransactionSnapshot ? context.RuntimeExecutionContext.CreateTransactionSnapshot("loop-control") : null,
            OutputPreview = JsonSerializer.SerializeToElement(new
            {
                iterations = iterationCount,
                controlSignal
            }, JsonOptions)
        };

    private static MicroflowLoopExecutionResult MaxIterationsExceeded(MicroflowLoopExecutionContext context, int iterationCount)
        => Failed(RuntimeErrorCode.RuntimeLoopMaxIterationsExceeded, $"Loop exceeded maxIterations={context.Options.MaxIterations}.", context.LoopNode, MicroflowLoopExecutionStatus.MaxIterationsExceeded) with
        {
            IterationCount = iterationCount,
            TransactionSnapshot = context.Options.IncludeTransactionSnapshot ? context.RuntimeExecutionContext.CreateTransactionSnapshot("loop-max-iterations") : null
        };

    private static MicroflowLoopExecutionResult Failed(string code, string message, MicroflowExecutionNode loopNode, string status = MicroflowLoopExecutionStatus.Failed, MicroflowRuntimeErrorDto? existing = null, string? details = null)
        => Failed(existing ?? Error(code, message, loopNode, details), status);

    private static MicroflowLoopExecutionResult Failed(MicroflowRuntimeErrorDto error, string status = MicroflowLoopExecutionStatus.Failed)
        => new()
        {
            Status = status,
            Error = error,
            Diagnostics =
            [
                new MicroflowExecutionDiagnosticDto
                {
                    Code = error.Code,
                    Severity = "error",
                    Message = error.Message,
                    ObjectId = error.ObjectId,
                    ActionId = error.ActionId,
                    FlowId = error.FlowId
                }
            ],
            OutputPreview = JsonSerializer.SerializeToElement(new
            {
                error.Code,
                error.Message
            }, JsonOptions)
        };

    private static MicroflowRuntimeErrorDto Error(string code, string message, MicroflowExecutionNode loopNode, string? details = null)
        => new()
        {
            Code = code,
            Message = message,
            ObjectId = loopNode.ObjectId,
            ActionId = loopNode.ActionId,
            Details = details
        };
}
