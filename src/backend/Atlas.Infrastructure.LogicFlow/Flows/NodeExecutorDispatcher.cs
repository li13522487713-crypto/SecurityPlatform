using System.Text.Json;
using Atlas.Application.LogicFlow.Flows.Abstractions;
using Atlas.Application.LogicFlow.Flows.Models;
using Atlas.Core.Expressions;
using Atlas.Infrastructure.LogicFlow.Expressions;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.Http.Json;

namespace Atlas.Infrastructure.LogicFlow.Flows;

public sealed class NodeExecutorDispatcher : INodeExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly HttpClient SharedHttpClient = new();
    private readonly ILogger<NodeExecutorDispatcher> _logger;
    private readonly ExprEvaluator _exprEvaluator;

    public NodeExecutorDispatcher(ILogger<NodeExecutorDispatcher> logger, ExprEvaluator exprEvaluator)
    {
        _logger = logger;
        _exprEvaluator = exprEvaluator;
    }

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var nodeKey = request.NodeKey;
        try
        {
            if (request.RetryAttempt > request.MaxRetries)
            {
                sw.Stop();
                return new NodeExecutionResult
                {
                    NodeKey = nodeKey,
                    IsSuccess = false,
                    ErrorMessage = "已超过最大重试次数",
                    DurationMs = sw.ElapsedMilliseconds,
                };
            }

            if (request.RetryAttempt > 0)
            {
                var delayMs = (int)Math.Min(30_000, 100 * Math.Pow(2, request.RetryAttempt - 1));
                await Task.Delay(delayMs, cancellationToken);
            }

            var timeoutSec = request.TimeoutSeconds > 0 ? request.TimeoutSeconds : 300;
            using var nodeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            nodeCts.CancelAfter(TimeSpan.FromSeconds(timeoutSec));

            _logger.LogInformation(
                "逻辑流节点执行(透传): execution={ExecutionId} node={NodeKey} type={TypeKey}",
                request.FlowExecutionId,
                nodeKey,
                request.TypeKey);

            var output = await ExecuteByNodeTypeAsync(request, nodeCts.Token);

            sw.Stop();
            return new NodeExecutionResult
            {
                NodeKey = nodeKey,
                IsSuccess = true,
                OutputData = output,
                DurationMs = sw.ElapsedMilliseconds,
            };
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return new NodeExecutionResult
            {
                NodeKey = nodeKey,
                IsSuccess = false,
                ErrorMessage = "节点执行超时或已取消",
                DurationMs = sw.ElapsedMilliseconds,
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "逻辑流节点执行失败: node={NodeKey}", nodeKey);
            return new NodeExecutionResult
            {
                NodeKey = nodeKey,
                IsSuccess = false,
                ErrorMessage = ex.Message,
                DurationMs = sw.ElapsedMilliseconds,
            };
        }
    }

    private async Task<Dictionary<string, string>> ExecuteByNodeTypeAsync(
        NodeExecutionRequest request,
        CancellationToken cancellationToken)
    {
        var typeKey = request.TypeKey.Trim().ToLowerInvariant();
        return typeKey switch
        {
            "trigger.manual" or "trigger.schedule" or "trigger.webhook" or "trigger.event"
                => ExecuteTriggerNode(request),
            "control.condition" => ExecuteConditionNode(request),
            "control.wait" => await ExecuteWaitNodeAsync(request, cancellationToken),
            "transform.expression" => ExecuteExpressionNode(request),
            "transform.field_mapping" => ExecuteFieldMappingNode(request),
            "sys.http_request" => await ExecuteHttpRequestNodeAsync(request, cancellationToken),
            _ => ExecutePassthroughNode(request)
        };
    }

    private static Dictionary<string, string> ExecuteTriggerNode(NodeExecutionRequest request)
    {
        var output = new Dictionary<string, string>(request.InputData, StringComparer.Ordinal)
        {
            ["triggeredAt"] = DateTimeOffset.UtcNow.ToString("O")
        };
        return output;
    }

    private Dictionary<string, string> ExecuteConditionNode(NodeExecutionRequest request)
    {
        var expression = ReadConfigString(request.ConfigJson, "expression", "false");
        var variables = BuildExpressionVariables(request.InputData);
        var evaluated = EvaluateExpression(expression, variables);
        var condition = ConvertToBoolean(evaluated);

        var output = new Dictionary<string, string>(request.InputData, StringComparer.Ordinal)
        {
            ["conditionResult"] = condition ? "true" : "false",
            ["branch"] = condition ? "true" : "false"
        };
        return output;
    }

    private async Task<Dictionary<string, string>> ExecuteWaitNodeAsync(
        NodeExecutionRequest request,
        CancellationToken cancellationToken)
    {
        var waitSeconds = ReadConfigInt(request.ConfigJson, "waitSeconds");
        var waitMs = ReadConfigInt(request.ConfigJson, "waitMs");
        var delayMs = waitMs > 0 ? waitMs : waitSeconds > 0 ? waitSeconds * 1000 : 0;
        delayMs = Math.Clamp(delayMs, 0, 60_000);

        if (delayMs > 0)
        {
            await Task.Delay(delayMs, cancellationToken);
        }

        var output = new Dictionary<string, string>(request.InputData, StringComparer.Ordinal)
        {
            ["waitMs"] = delayMs.ToString()
        };
        return output;
    }

    private Dictionary<string, string> ExecuteExpressionNode(NodeExecutionRequest request)
    {
        var expression = ReadConfigString(request.ConfigJson, "expression", string.Empty);
        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new InvalidOperationException("transform.expression 缺少 expression 配置。");
        }

        var outputKey = ReadConfigString(request.ConfigJson, "outputKey", "result");
        var variables = BuildExpressionVariables(request.InputData);
        var result = EvaluateExpression(expression, variables);

        var output = new Dictionary<string, string>(request.InputData, StringComparer.Ordinal)
        {
            [outputKey] = ToOutputString(result)
        };
        return output;
    }

    private Dictionary<string, string> ExecuteFieldMappingNode(NodeExecutionRequest request)
    {
        var mappings = ReadMappingConfig(request.ConfigJson);
        var variables = BuildExpressionVariables(request.InputData);
        var output = new Dictionary<string, string>(request.InputData, StringComparer.Ordinal);
        foreach (var mapping in mappings)
        {
            if (string.IsNullOrWhiteSpace(mapping.Key) || string.IsNullOrWhiteSpace(mapping.Value))
            {
                continue;
            }

            var value = EvaluateExpression(mapping.Value, variables);
            output[mapping.Key] = ToOutputString(value);
        }

        return output;
    }

    private async Task<Dictionary<string, string>> ExecuteHttpRequestNodeAsync(
        NodeExecutionRequest request,
        CancellationToken cancellationToken)
    {
        var url = ReadConfigString(request.ConfigJson, "url", string.Empty);
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("sys.http_request 缺少 url 配置。");
        }

        var method = ReadConfigString(request.ConfigJson, "method", "GET").ToUpperInvariant();
        using var message = new HttpRequestMessage(new HttpMethod(method), url);
        var body = ReadConfigString(request.ConfigJson, "body", string.Empty);
        if (!string.IsNullOrWhiteSpace(body) && method is "POST" or "PUT" or "PATCH")
        {
            message.Content = JsonContent.Create(new { body });
        }

        var response = await SharedHttpClient.SendAsync(message, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var output = new Dictionary<string, string>(request.InputData, StringComparer.Ordinal)
        {
            ["statusCode"] = ((int)response.StatusCode).ToString(),
            ["responseBody"] = responseBody
        };
        return output;
    }

    private static Dictionary<string, string> ExecutePassthroughNode(NodeExecutionRequest request)
    {
        var inputJson = JsonSerializer.Serialize(request.InputData, JsonOptions);
        var output = new Dictionary<string, string>(request.InputData, StringComparer.Ordinal);
        if (!output.ContainsKey("echo"))
        {
            output["echo"] = inputJson;
        }
        return output;
    }

    private object? EvaluateExpression(string expression, IReadOnlyDictionary<string, object?> variables)
    {
        var ast = _exprEvaluator.ParseAndCache(expression);
        var context = ExpressionContext.FromRecord(variables);
        return _exprEvaluator.Evaluate(ast, context);
    }

    private static Dictionary<string, object?> BuildExpressionVariables(Dictionary<string, string> inputData)
    {
        var variables = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in inputData)
        {
            variables[pair.Key] = TryParseJson(pair.Value);
        }

        if (variables.TryGetValue("flowInput", out var flowObj) && flowObj is Dictionary<string, object?> flowMap)
        {
            foreach (var pair in flowMap)
            {
                variables[pair.Key] = pair.Value;
            }
        }

        if (variables.TryGetValue("nodeInput", out var nodeObj) && nodeObj is Dictionary<string, object?> nodeMap)
        {
            foreach (var pair in nodeMap)
            {
                variables[pair.Key] = pair.Value;
            }
        }

        return variables;
    }

    private static object? TryParseJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            return ConvertJsonValue(document.RootElement);
        }
        catch
        {
            return raw;
        }
    }

    private static object? ConvertJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(
                    property => property.Name,
                    property => ConvertJsonValue(property.Value),
                    StringComparer.OrdinalIgnoreCase),
            JsonValueKind.Array => element.EnumerateArray()
                .Select(ConvertJsonValue)
                .ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }

    private static bool ConvertToBoolean(object? value)
    {
        return value switch
        {
            null => false,
            bool b => b,
            string s when bool.TryParse(s, out var parsedBool) => parsedBool,
            string s when long.TryParse(s, out var parsedLong) => parsedLong != 0,
            long l => l != 0,
            int i => i != 0,
            double d => Math.Abs(d) > double.Epsilon,
            _ => true
        };
    }

    private static string ToOutputString(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string s => s,
            bool b => b ? "true" : "false",
            int i => i.ToString(),
            long l => l.ToString(),
            double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
            decimal d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => JsonSerializer.Serialize(value, JsonOptions)
        };
    }

    private static string ReadConfigString(string configJson, string propertyName, string defaultValue)
    {
        if (TryReadConfigValue(configJson, propertyName, out var value))
        {
            return value;
        }

        return defaultValue;
    }

    private static int ReadConfigInt(string configJson, string propertyName)
    {
        if (!TryReadConfigValue(configJson, propertyName, out var value))
        {
            return 0;
        }

        return int.TryParse(value, out var parsed) ? parsed : 0;
    }

    private static Dictionary<string, string> ReadMappingConfig(string configJson)
    {
        var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson);
            if (!document.RootElement.TryGetProperty("mappings", out var mappingsElement) ||
                mappingsElement.ValueKind != JsonValueKind.Object)
            {
                return mappings;
            }

            foreach (var property in mappingsElement.EnumerateObject())
            {
                mappings[property.Name] = property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString() ?? string.Empty
                    : property.Value.GetRawText();
            }
        }
        catch
        {
            // Ignore invalid mapping config and keep passthrough behavior.
        }

        return mappings;
    }

    private static bool TryReadConfigValue(string configJson, string propertyName, out string value)
    {
        value = string.Empty;
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson);
            if (!document.RootElement.TryGetProperty(propertyName, out var element))
            {
                return false;
            }

            value = element.ValueKind == JsonValueKind.String ? element.GetString() ?? string.Empty : element.GetRawText();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
