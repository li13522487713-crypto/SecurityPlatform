using System.Text.Json;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>
/// 数据库查询节点：当前版本为占位实现。
/// Config 参数：query、databaseId、outputKey
/// </summary>
public sealed class DatabaseQueryNodeExecutor : INodeExecutor
{
    public WorkflowNodeType NodeType => WorkflowNodeType.DatabaseQuery;

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputKey = context.GetConfigString("outputKey", "query_result");
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            [outputKey] = JsonSerializer.SerializeToElement(Array.Empty<object>())
        };

        // 占位：TODO[coze-v2-db-query] 集成 AiDatabase 数据源执行查询
        return Task.FromResult(new NodeExecutionResult(true, outputs));
    }
}
