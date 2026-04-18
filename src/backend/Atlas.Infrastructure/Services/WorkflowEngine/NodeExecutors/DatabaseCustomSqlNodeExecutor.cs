using System.Text.Json;
using Atlas.Domain.AiPlatform.Enums;
using SqlSugar;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>
/// 数据库 SQL 路由节点：仅根据 <c>sqlTemplate</c> 的首个关键字（SELECT/INSERT/UPDATE/DELETE）
/// 转发到对应的标准数据库节点；<b>不解析、不执行</b> sqlTemplate 正文。用于兼容「看起来像 SQL」的画布配置。
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
            return Task.FromResult(new NodeExecutionResult(false, new Dictionary<string, JsonElement>(), "DatabaseSqlRoute 缺少 sqlTemplate。"));
        }

        if (ContainsDangerousToken(sql))
        {
            return Task.FromResult(new NodeExecutionResult(false, new Dictionary<string, JsonElement>(), "DatabaseSqlRoute 检测到潜在危险片段（如注释/分号/DROP 等）。"));
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

        return Task.FromResult(new NodeExecutionResult(false, new Dictionary<string, JsonElement>(), "DatabaseSqlRoute 仅支持以 SELECT/INSERT/UPDATE/DELETE 开头的路由关键字。"));
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
