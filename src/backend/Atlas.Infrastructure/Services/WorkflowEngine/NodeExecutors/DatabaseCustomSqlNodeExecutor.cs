using System.Text.Json;
using Atlas.Domain.AiPlatform.Enums;
using SqlSugar;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>
/// 自定义 SQL 节点：限制执行受控语句，避免注入与高危操作。
/// </summary>
public sealed class DatabaseCustomSqlNodeExecutor : INodeExecutor
{
    private readonly DatabaseQueryNodeExecutor _queryExecutor;
    private readonly DatabaseUpdateNodeExecutor _updateExecutor;
    private readonly DatabaseDeleteNodeExecutor _deleteExecutor;
    private readonly DatabaseInsertNodeExecutor _insertExecutor;

    public DatabaseCustomSqlNodeExecutor(
        ISqlSugarClient db,
        Atlas.Core.Abstractions.IIdGeneratorAccessor idGeneratorAccessor)
    {
        _queryExecutor = new DatabaseQueryNodeExecutor(db);
        _updateExecutor = new DatabaseUpdateNodeExecutor(db);
        _deleteExecutor = new DatabaseDeleteNodeExecutor(db);
        _insertExecutor = new DatabaseInsertNodeExecutor(db, idGeneratorAccessor);
    }

    public WorkflowNodeType NodeType => WorkflowNodeType.DatabaseCustomSql;

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var sqlTemplate = context.GetConfigString("sqlTemplate");
        var sql = context.ReplaceVariables(sqlTemplate).Trim();
        if (string.IsNullOrWhiteSpace(sql))
        {
            return Task.FromResult(new NodeExecutionResult(false, new Dictionary<string, JsonElement>(), "DatabaseCustomSql 缺少 sqlTemplate。"));
        }

        if (ContainsDangerousToken(sql))
        {
            return Task.FromResult(new NodeExecutionResult(false, new Dictionary<string, JsonElement>(), "DatabaseCustomSql 检测到潜在危险语句。"));
        }

        if (sql.StartsWith("select", StringComparison.OrdinalIgnoreCase))
        {
            return _queryExecutor.ExecuteAsync(context, cancellationToken);
        }

        if (sql.StartsWith("update", StringComparison.OrdinalIgnoreCase))
        {
            return _updateExecutor.ExecuteAsync(context, cancellationToken);
        }

        if (sql.StartsWith("delete", StringComparison.OrdinalIgnoreCase))
        {
            return _deleteExecutor.ExecuteAsync(context, cancellationToken);
        }

        if (sql.StartsWith("insert", StringComparison.OrdinalIgnoreCase))
        {
            return _insertExecutor.ExecuteAsync(context, cancellationToken);
        }

        return Task.FromResult(new NodeExecutionResult(false, new Dictionary<string, JsonElement>(), "DatabaseCustomSql 仅支持 SELECT/INSERT/UPDATE/DELETE。"));
    }

    private static bool ContainsDangerousToken(string sql)
    {
        return sql.Contains(';') ||
               sql.Contains("--", StringComparison.Ordinal) ||
               sql.Contains("/*", StringComparison.Ordinal) ||
               sql.Contains("xp_", StringComparison.OrdinalIgnoreCase) ||
               sql.Contains("drop ", StringComparison.OrdinalIgnoreCase) ||
               sql.Contains("truncate ", StringComparison.OrdinalIgnoreCase) ||
               sql.Contains("alter ", StringComparison.OrdinalIgnoreCase);
    }
}
