using System.Text.Json;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Infrastructure.Services.AiPlatform;

/// <summary>
/// Shared JSON helper used by all node-config adapters.
/// </summary>
internal static class CozeAdapterHelper
{
    internal static string? TryGetString(JsonElement element, string propertyName)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.String)
            {
                return property.Value.GetString();
            }
        }

        return null;
    }
}

internal interface ICozeNodeConfigAdapter
{
    WorkflowNodeType NodeType { get; }

    void Adapt(in CozeNodeAdaptContext context, Dictionary<string, JsonElement> config);
}

internal readonly struct CozeNodeAdaptContext
{
    public CozeNodeAdaptContext(
        JsonElement nodeElement,
        JsonElement dataElement,
        string nodeKey,
        Dictionary<string, string> inputMappings)
    {
        NodeElement = nodeElement;
        DataElement = dataElement;
        NodeKey = nodeKey;
        InputMappings = inputMappings;
    }

    public JsonElement NodeElement { get; }
    public JsonElement DataElement { get; }
    public string NodeKey { get; }

    /// <summary>
    /// Mutable input-mapping table; adapters may add branch-ref entries here.
    /// </summary>
    public Dictionary<string, string> InputMappings { get; }
}

internal static class CozeNodeConfigAdapterRegistry
{
    private static readonly IReadOnlyDictionary<WorkflowNodeType, ICozeNodeConfigAdapter> AdapterMap =
        new ICozeNodeConfigAdapter[]
        {
            new EntryNodeConfigAdapter(),
            new ExitNodeConfigAdapter(),
            new CodeRunnerNodeConfigAdapter(),
            new HttpRequesterNodeConfigAdapter(),
            new SelectorNodeConfigAdapter(),
            new LoopNodeConfigAdapter(),
            new LlmNodeConfigAdapter(),
            new TextProcessorNodeConfigAdapter(),
            new CommonOutputsNodeConfigAdapter(WorkflowNodeType.SubWorkflow)
        }.ToDictionary(x => x.NodeType, x => x);

    public static void Adapt(WorkflowNodeType nodeType, in CozeNodeAdaptContext context, Dictionary<string, JsonElement> config)
    {
        if (AdapterMap.TryGetValue(nodeType, out var adapter))
        {
            adapter.Adapt(context, config);
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Entry
// ─────────────────────────────────────────────────────────────────────────────

internal sealed class EntryNodeConfigAdapter : ICozeNodeConfigAdapter
{
    public WorkflowNodeType NodeType => WorkflowNodeType.Entry;

    public void Adapt(in CozeNodeAdaptContext context, Dictionary<string, JsonElement> config)
    {
        if (!context.DataElement.TryGetProperty("outputs", out var outputsElement) ||
            outputsElement.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var outputList = new List<object>();
        var firstVarName = (string?)null;

        foreach (var item in outputsElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var name = CozeAdapterHelper.TryGetString(item, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var varName = name!;
            firstVarName ??= varName;

            var typeName = CozeAdapterHelper.TryGetString(item, "type") ?? "String";
            var defaultValue = item.TryGetProperty("defaultValue", out var dv) ? (object?)dv.Clone() : null;

            outputList.Add(new { name = varName, type = typeName, defaultValue });
        }

        if (outputList.Count > 0)
        {
            config["entryOutputs"] = JsonSerializer.SerializeToElement(outputList);
        }

        if (!string.IsNullOrWhiteSpace(firstVarName))
        {
            config["entryVariable"] = JsonSerializer.SerializeToElement(firstVarName);
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Exit
// ─────────────────────────────────────────────────────────────────────────────

internal sealed class ExitNodeConfigAdapter : ICozeNodeConfigAdapter
{
    public WorkflowNodeType NodeType => WorkflowNodeType.Exit;

    public void Adapt(in CozeNodeAdaptContext context, Dictionary<string, JsonElement> config)
    {
        if (!context.DataElement.TryGetProperty("inputs", out var exitInputs) ||
            exitInputs.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        // terminatePlan → exitTerminateMode
        if (exitInputs.TryGetProperty("terminatePlan", out var terminatePlan) &&
            terminatePlan.ValueKind == JsonValueKind.String)
        {
            var mode = terminatePlan.GetString()?.Trim();
            if (!string.IsNullOrWhiteSpace(mode))
            {
                config["exitTerminateMode"] = JsonSerializer.SerializeToElement(mode);
            }
        }

        // outputEmitter.streamingOutput → streamingOutput
        // outputEmitter.content.value  → exitTemplate (preferred over inputParameters)
        if (exitInputs.TryGetProperty("outputEmitter", out var outputEmitter) &&
            outputEmitter.ValueKind == JsonValueKind.Object)
        {
            if (outputEmitter.TryGetProperty("streamingOutput", out var streamingOutput))
            {
                config["streamingOutput"] = streamingOutput.Clone();
            }

            if (outputEmitter.TryGetProperty("content", out var content) &&
                content.ValueKind == JsonValueKind.Object &&
                content.TryGetProperty("value", out var contentValue))
            {
                // content.value may be a template string or a BlockInput object
                if (contentValue.ValueKind == JsonValueKind.String)
                {
                    var template = contentValue.GetString();
                    if (!string.IsNullOrWhiteSpace(template))
                    {
                        config["exitTemplate"] = JsonSerializer.SerializeToElement(template);
                    }
                }
                else
                {
                    config["exitTemplate"] = contentValue.Clone();
                }
            }
        }

        // Fallback to inputParameters-based template extraction
        if (!config.ContainsKey("exitTemplate") &&
            exitInputs.TryGetProperty("inputParameters", out var inputParameters) &&
            inputParameters.ValueKind == JsonValueKind.Array)
        {
            var template = CozeWorkflowPlanCompiler.ExtractFirstExpressionContent(inputParameters);
            if (!string.IsNullOrWhiteSpace(template))
            {
                config["exitTemplate"] = JsonSerializer.SerializeToElement(template);
            }
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// CodeRunner
// ─────────────────────────────────────────────────────────────────────────────

internal sealed class CodeRunnerNodeConfigAdapter : ICozeNodeConfigAdapter
{
    public WorkflowNodeType NodeType => WorkflowNodeType.CodeRunner;

    public void Adapt(in CozeNodeAdaptContext context, Dictionary<string, JsonElement> config)
    {
        if (context.DataElement.TryGetProperty("inputs", out var codeInputs) &&
            codeInputs.ValueKind == JsonValueKind.Object)
        {
            if (codeInputs.TryGetProperty("code", out var codeElement))
            {
                config["code"] = codeElement.Clone();
            }

            if (codeInputs.TryGetProperty("language", out var languageElement))
            {
                config["language"] = languageElement.Clone();
            }

            if (codeInputs.TryGetProperty("inputParameters", out var inputParameters) &&
                inputParameters.ValueKind == JsonValueKind.Array)
            {
                config["inputParameters"] = inputParameters.Clone();
            }
        }

        if (context.DataElement.TryGetProperty("outputs", out var codeOutputs) &&
            codeOutputs.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in codeOutputs.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var outputName = CozeAdapterHelper.TryGetString(item, "name")?.Trim();
                if (string.IsNullOrWhiteSpace(outputName))
                {
                    continue;
                }

                config["outputKey"] = JsonSerializer.SerializeToElement(outputName);
                break;
            }
        }

        if (!config.ContainsKey("outputKey"))
        {
            config["outputKey"] = JsonSerializer.SerializeToElement($"{context.NodeKey}_output");
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Selector (If / Condition)
// ─────────────────────────────────────────────────────────────────────────────

internal sealed class SelectorNodeConfigAdapter : ICozeNodeConfigAdapter
{
    public WorkflowNodeType NodeType => WorkflowNodeType.Selector;

    public void Adapt(in CozeNodeAdaptContext context, Dictionary<string, JsonElement> config)
    {
        if (!context.DataElement.TryGetProperty("inputs", out var inputs) ||
            inputs.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        // Coze branches live under inputs.branches[]
        // Fall back to inputs.conditions[] for older schemas
        var branchesSource =
            inputs.TryGetProperty("branches", out var branchesEl) && branchesEl.ValueKind == JsonValueKind.Array
                ? branchesEl
                : inputs.TryGetProperty("conditions", out var conditionsEl) && conditionsEl.ValueKind == JsonValueKind.Array
                    ? conditionsEl
                    : (JsonElement?)null;

        if (branchesSource is null)
        {
            if (context.DataElement.TryGetProperty("outputs", out var outputs) &&
                outputs.ValueKind == JsonValueKind.Array)
            {
                config["outputs"] = outputs.Clone();
            }

            return;
        }

        var branchList = new List<object>();
        var branchIndex = 0;

        foreach (var branch in branchesSource.Value.EnumerateArray())
        {
            if (branch.ValueKind != JsonValueKind.Object)
            {
                branchIndex++;
                continue;
            }

            // Resolve condition block: branch.condition or branch itself
            var conditionBlock =
                branch.TryGetProperty("condition", out var condProp) && condProp.ValueKind == JsonValueKind.Object
                    ? condProp
                    : branch;

            var logic = "AND";
            if (conditionBlock.TryGetProperty("logic", out var logicEl) &&
                logicEl.ValueKind == JsonValueKind.String)
            {
                logic = logicEl.GetString()?.ToUpperInvariant() ?? "AND";
            }

            var conditionItems = new List<object>();

            if (conditionBlock.TryGetProperty("conditions", out var condList) &&
                condList.ValueKind == JsonValueKind.Array)
            {
                foreach (var cond in condList.EnumerateArray())
                {
                    if (cond.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var op = CozeAdapterHelper.TryGetString(cond, "operator") ?? "equal";
                    var leftRef = ExtractBlockInputRef(cond, "left", context.InputMappings, branchIndex);
                    var rightRef = ExtractBlockInputRef(cond, "right", context.InputMappings, branchIndex);
                    var rightLiteral = ExtractBlockInputLiteral(cond, "right");

                    conditionItems.Add(new
                    {
                        @operator = op,
                        leftRef,
                        rightRef,
                        rightLiteral
                    });
                }
            }

            branchList.Add(new { branchIndex, logic, conditions = conditionItems });
            branchIndex++;
        }

        if (branchList.Count > 0)
        {
            config["branches"] = JsonSerializer.SerializeToElement(branchList);
        }

        // Propagate outputs for downstream resolution
        if (context.DataElement.TryGetProperty("outputs", out var selectorOutputs) &&
            selectorOutputs.ValueKind == JsonValueKind.Array)
        {
            config["outputs"] = selectorOutputs.Clone();
        }
    }

    private static string? ExtractBlockInputRef(
        JsonElement cond,
        string side,
        Dictionary<string, string> inputMappings,
        int branchIndex)
    {
        if (!cond.TryGetProperty(side, out var sideEl) || sideEl.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!sideEl.TryGetProperty("value", out var valueEl) || valueEl.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var type = CozeAdapterHelper.TryGetString(valueEl, "type");
        if (!string.Equals(type, "ref", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!valueEl.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var blockId = CozeAdapterHelper.TryGetString(content, "blockID");
        var name = CozeAdapterHelper.TryGetString(content, "name");
        if (string.IsNullOrWhiteSpace(blockId) || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var refPath = $"{blockId}.{name}";
        var mappingKey = $"branch_{branchIndex}_{side}";
        inputMappings[mappingKey] = refPath;
        return refPath;
    }

    private static object? ExtractBlockInputLiteral(JsonElement cond, string side)
    {
        if (!cond.TryGetProperty(side, out var sideEl) || sideEl.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!sideEl.TryGetProperty("value", out var valueEl) || valueEl.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var type = CozeAdapterHelper.TryGetString(valueEl, "type");
        if (string.Equals(type, "ref", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (valueEl.TryGetProperty("content", out var content))
        {
            return content.ValueKind == JsonValueKind.String ? (object?)content.GetString() : (object?)content.Clone();
        }

        return null;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Loop
// ─────────────────────────────────────────────────────────────────────────────

internal sealed class LoopNodeConfigAdapter : ICozeNodeConfigAdapter
{
    public WorkflowNodeType NodeType => WorkflowNodeType.Loop;

    public void Adapt(in CozeNodeAdaptContext context, Dictionary<string, JsonElement> config)
    {
        if (!context.DataElement.TryGetProperty("inputs", out var inputs) ||
            inputs.ValueKind != JsonValueKind.Object)
        {
            CopyOutputs(context, config);
            return;
        }

        // loopType: "array" | "count" | "infinite"
        if (inputs.TryGetProperty("loopType", out var loopTypeEl))
        {
            config["loopType"] = loopTypeEl.Clone();
        }

        // loopCount: BlockInput (may be a ref or literal number)
        if (inputs.TryGetProperty("loopCount", out var loopCountEl))
        {
            config["loopCount"] = loopCountEl.Clone();
        }

        // variableParameters: intermediate loop variables
        if (inputs.TryGetProperty("variableParameters", out var varParams) &&
            varParams.ValueKind == JsonValueKind.Array)
        {
            var varNames = new List<string>();
            foreach (var vp in varParams.EnumerateArray())
            {
                if (vp.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var name = CozeAdapterHelper.TryGetString(vp, "name");
                if (!string.IsNullOrWhiteSpace(name))
                {
                    varNames.Add(name!);
                }
            }

            config["intermediateVars"] = JsonSerializer.SerializeToElement(varNames);
        }

        CopyOutputs(context, config);
    }

    private static void CopyOutputs(in CozeNodeAdaptContext context, Dictionary<string, JsonElement> config)
    {
        if (context.DataElement.TryGetProperty("outputs", out var outputs) &&
            outputs.ValueKind == JsonValueKind.Array)
        {
            config["outputs"] = outputs.Clone();
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// LLM
// ─────────────────────────────────────────────────────────────────────────────

internal sealed class LlmNodeConfigAdapter : ICozeNodeConfigAdapter
{
    public WorkflowNodeType NodeType => WorkflowNodeType.Llm;

    public void Adapt(in CozeNodeAdaptContext context, Dictionary<string, JsonElement> config)
    {
        if (context.DataElement.TryGetProperty("outputs", out var outputsElement) &&
            outputsElement.ValueKind == JsonValueKind.Array)
        {
            config["outputs"] = outputsElement.Clone();
        }

        if (!context.DataElement.TryGetProperty("inputs", out var inputs) ||
            inputs.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        // llmParam is an array of {name, input:{value:{type,content}}} entries
        if (inputs.TryGetProperty("llmParam", out var llmParam) &&
            llmParam.ValueKind == JsonValueKind.Array)
        {
            foreach (var param in llmParam.EnumerateArray())
            {
                if (param.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var paramName = CozeAdapterHelper.TryGetString(param, "name");
                if (string.IsNullOrWhiteSpace(paramName))
                {
                    continue;
                }

                // Extract scalar value: input.value.content (literal string/number) or input.value itself
                var scalarValue = ExtractLlmParamValue(param);
                if (scalarValue is null)
                {
                    continue;
                }

                switch (paramName.Trim().ToLowerInvariant())
                {
                    // Coze has a typo "modleName"; handle both
                    case "modlename":
                    case "modelname":
                        config["modelName"] = scalarValue.Value;
                        break;
                    case "modeltype":
                        config["modelType"] = scalarValue.Value;
                        break;
                    case "temperature":
                        config["temperature"] = scalarValue.Value;
                        break;
                    case "maxtokens":
                    case "max_tokens":
                        config["maxTokens"] = scalarValue.Value;
                        break;
                    case "systemprompt":
                        config["systemPrompt"] = scalarValue.Value;
                        break;
                    case "prompt":
                    case "userprompt":
                        config["prompt"] = scalarValue.Value;
                        config["userPrompt"] = scalarValue.Value;
                        break;
                }
            }
        }

        // fcParam (function-call / tool-calling configuration)
        if (inputs.TryGetProperty("fcParam", out var fcParam))
        {
            config["fcParam"] = fcParam.Clone();
        }

        // settingOnError.ext.backupLLMParam (optional fallback model)
        if (inputs.TryGetProperty("settingOnError", out var settingOnError) &&
            settingOnError.ValueKind == JsonValueKind.Object &&
            settingOnError.TryGetProperty("ext", out var ext) &&
            ext.ValueKind == JsonValueKind.Object &&
            ext.TryGetProperty("backupLLMParam", out var backupLlm))
        {
            config["backupLLMParam"] = backupLlm.Clone();
        }
    }

    private static JsonElement? ExtractLlmParamValue(JsonElement param)
    {
        // Pattern: param.input.value.content (literal)
        if (param.TryGetProperty("input", out var inputEl) &&
            inputEl.ValueKind == JsonValueKind.Object &&
            inputEl.TryGetProperty("value", out var valueEl) &&
            valueEl.ValueKind == JsonValueKind.Object)
        {
            if (valueEl.TryGetProperty("content", out var content))
            {
                return content.Clone();
            }

            // Fallback: return the whole value object
            return valueEl.Clone();
        }

        // Direct value property on param
        if (param.TryGetProperty("value", out var directVal))
        {
            return directVal.Clone();
        }

        return null;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// HttpRequester
// ─────────────────────────────────────────────────────────────────────────────

internal sealed class HttpRequesterNodeConfigAdapter : ICozeNodeConfigAdapter
{
    public WorkflowNodeType NodeType => WorkflowNodeType.HttpRequester;

    public void Adapt(in CozeNodeAdaptContext context, Dictionary<string, JsonElement> config)
    {
        if (context.DataElement.TryGetProperty("outputs", out var outputsElement) &&
            outputsElement.ValueKind == JsonValueKind.Array)
        {
            config["outputs"] = outputsElement.Clone();
        }

        if (!context.DataElement.TryGetProperty("inputs", out var inputs) ||
            inputs.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        // apiInfo.{method, url}
        if (inputs.TryGetProperty("apiInfo", out var apiInfo) &&
            apiInfo.ValueKind == JsonValueKind.Object)
        {
            if (apiInfo.TryGetProperty("method", out var method))
            {
                config["method"] = method.Clone();
            }

            if (apiInfo.TryGetProperty("url", out var url))
            {
                config["url"] = url.Clone();
            }
        }

        // body.{bodyType, bodyData}
        if (inputs.TryGetProperty("body", out var body) &&
            body.ValueKind == JsonValueKind.Object)
        {
            if (body.TryGetProperty("bodyType", out var bodyType))
            {
                config["bodyType"] = bodyType.Clone();
            }

            if (body.TryGetProperty("bodyData", out var bodyData))
            {
                config["bodyData"] = bodyData.Clone();
            }
        }

        // headers (array of {key, value} BlockInput pairs)
        if (inputs.TryGetProperty("headers", out var headers) &&
            headers.ValueKind == JsonValueKind.Array)
        {
            config["headers"] = headers.Clone();
        }

        // params (query parameters array)
        if (inputs.TryGetProperty("params", out var queryParams) &&
            queryParams.ValueKind == JsonValueKind.Array)
        {
            config["params"] = queryParams.Clone();
        }

        // auth: {authOpen, authType, authData}
        if (inputs.TryGetProperty("auth", out var auth) &&
            auth.ValueKind == JsonValueKind.Object)
        {
            config["auth"] = auth.Clone();
        }

        // setting.{timeout, retryTimes}
        if (inputs.TryGetProperty("setting", out var setting) &&
            setting.ValueKind == JsonValueKind.Object)
        {
            if (setting.TryGetProperty("timeout", out var timeout))
            {
                config["timeout"] = timeout.Clone();
            }

            if (setting.TryGetProperty("retryTimes", out var retryTimes))
            {
                config["retryTimes"] = retryTimes.Clone();
            }
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// TextProcessor
// ─────────────────────────────────────────────────────────────────────────────

internal sealed class TextProcessorNodeConfigAdapter : ICozeNodeConfigAdapter
{
    public WorkflowNodeType NodeType => WorkflowNodeType.TextProcessor;

    public void Adapt(in CozeNodeAdaptContext context, Dictionary<string, JsonElement> config)
    {
        if (!context.DataElement.TryGetProperty("inputs", out var inputs) ||
            inputs.ValueKind != JsonValueKind.Object)
        {
            CopyOutputKey(context, config);
            return;
        }

        if (inputs.TryGetProperty("concatParams", out var concatParams) &&
            concatParams.ValueKind == JsonValueKind.Array)
        {
            foreach (var param in concatParams.EnumerateArray())
            {
                if (param.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var name = CozeAdapterHelper.TryGetString(param, "name");
                if (!string.Equals(name, "concatResult", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var template = ExtractBlockInputContent(param);
                if (!string.IsNullOrWhiteSpace(template))
                {
                    config["template"] = JsonSerializer.SerializeToElement(template);
                    break;
                }
            }
        }

        CopyOutputKey(context, config);
    }

    private static string? ExtractBlockInputContent(JsonElement param)
    {
        if (!param.TryGetProperty("input", out var input) ||
            input.ValueKind != JsonValueKind.Object ||
            !input.TryGetProperty("value", out var value) ||
            value.ValueKind != JsonValueKind.Object ||
            !value.TryGetProperty("content", out var content))
        {
            return null;
        }

        return content.ValueKind == JsonValueKind.String ? content.GetString() : content.GetRawText();
    }

    private static void CopyOutputKey(in CozeNodeAdaptContext context, Dictionary<string, JsonElement> config)
    {
        if (!context.DataElement.TryGetProperty("outputs", out var outputsElement) ||
            outputsElement.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in outputsElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var outputName = CozeAdapterHelper.TryGetString(item, "name")?.Trim();
            if (string.IsNullOrWhiteSpace(outputName))
            {
                continue;
            }

            config["outputKey"] = JsonSerializer.SerializeToElement(outputName);
            return;
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// SubWorkflow (and any future node type that only needs outputs forwarded)
// ─────────────────────────────────────────────────────────────────────────────

internal sealed class CommonOutputsNodeConfigAdapter : ICozeNodeConfigAdapter
{
    public CommonOutputsNodeConfigAdapter(WorkflowNodeType nodeType)
    {
        NodeType = nodeType;
    }

    public WorkflowNodeType NodeType { get; }

    public void Adapt(in CozeNodeAdaptContext context, Dictionary<string, JsonElement> config)
    {
        if (context.DataElement.TryGetProperty("outputs", out var outputsElement) &&
            outputsElement.ValueKind == JsonValueKind.Array)
        {
            config["outputs"] = outputsElement.Clone();
        }
    }
}
