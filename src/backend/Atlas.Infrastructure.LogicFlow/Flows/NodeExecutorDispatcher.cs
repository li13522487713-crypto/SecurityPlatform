using System.Text.Json;
using Atlas.Application.LogicFlow.Flows.Abstractions;
using Atlas.Application.LogicFlow.Flows.Models;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.LogicFlow.Flows;

public sealed class NodeExecutorDispatcher : INodeExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ILogger<NodeExecutorDispatcher> _logger;

    public NodeExecutorDispatcher(ILogger<NodeExecutorDispatcher> logger)
    {
        _logger = logger;
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

            var inputJson = JsonSerializer.Serialize(request.InputData, JsonOptions);
            await Task.Delay(0, nodeCts.Token);

            var output = new Dictionary<string, string>(request.InputData, StringComparer.Ordinal);
            if (!output.ContainsKey("echo"))
                output["echo"] = inputJson;

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
}
