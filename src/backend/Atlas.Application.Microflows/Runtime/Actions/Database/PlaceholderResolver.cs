using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Atlas.Application.Microflows.Runtime.Security;

namespace Atlas.Application.Microflows.Runtime.Actions.Database;

/// <summary>
/// 将 SQL 中的微流变量占位符（$.x / $global.x / $currentUser.x）替换为
/// 方言参数化占位符（@p0, @p1 … 或 MySQL ?），并构建对应的参数列表。
/// 严禁字符串拼接，所有值必须以参数形式传递。
/// </summary>
public static class PlaceholderResolver
{
    // 匹配 $.varName / $varName（局部变量）、$global.varName（全局变量）或 $currentUser.fieldName（当前用户字段）
    // 协议统一使用 $.name / $global.name / $currentUser.name，点号在局部变量时可选（兼容历史写法）
    private static readonly Regex PlaceholderPattern = new(
        @"\$\.?(global\.[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*|currentUser\.[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*|[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// 解析 SQL 并替换所有占位符。
    /// </summary>
    /// <param name="sql">原始 SQL（含占位符）</param>
    /// <param name="driverCode">数据库驱动代码（MySql / PostgreSQL / SQLite / ...）</param>
    /// <param name="context">执行上下文，用于读取变量值</param>
    /// <returns>参数化后的 SQL 和对应参数列表</returns>
    public static (string ParameterizedSql, IReadOnlyList<MicroflowDatabaseSqlParameter> Parameters) Resolve(
        string sql,
        string driverCode,
        MicroflowActionExecutionContext context)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return (sql, Array.Empty<MicroflowDatabaseSqlParameter>());
        }

        var parameters = new List<MicroflowDatabaseSqlParameter>();
        var paramIndex = 0;
        var sb = new StringBuilder();
        var lastIndex = 0;
        var useMysqlStyle = IsMysqlStyleDriver(driverCode);

        foreach (Match match in PlaceholderPattern.Matches(sql))
        {
            // 跳过 SQL 字符串字面量内部的占位符（简单启发式：检查前方引号数量的奇偶性）
            if (IsInsideStringLiteral(sql, match.Index))
            {
                continue;
            }

            sb.Append(sql, lastIndex, match.Index - lastIndex);

            var path = match.Groups[1].Value; // 如 "orderId" 或 "currentUser.id" 或 "global.userList"
            var value = ResolveValue(path, context);

            var paramName = useMysqlStyle ? "?" : $"@p{paramIndex}";
            sb.Append(paramName);
            parameters.Add(new MicroflowDatabaseSqlParameter($"@p{paramIndex}", value));
            paramIndex++;
            lastIndex = match.Index + match.Length;
        }

        sb.Append(sql, lastIndex, sql.Length - lastIndex);
        return (sb.ToString(), parameters);
    }

    private static object? ResolveValue(string path, MicroflowActionExecutionContext context)
    {
        // $currentUser.fieldName
        if (path.StartsWith("currentUser.", StringComparison.Ordinal))
        {
            var fieldName = path["currentUser.".Length..];
            return ResolveCurrentUserField(fieldName, context.RuntimeSecurityContext);
        }

        // $global.varName(.subPath)
        if (path.StartsWith("global.", StringComparison.Ordinal))
        {
            var varPath = path["global.".Length..];
            return ResolveVariablePath(varPath, context);
        }

        // $.varName(.subPath)
        return ResolveVariablePath(path, context);
    }

    private static object? ResolveCurrentUserField(string fieldName, MicroflowRuntimeSecurityContext security)
        => fieldName switch
        {
            "id" or "userId" => (object?)security.UserId,
            "name" or "userName" => security.UserName,
            "tenantId" => security.TenantId,
            "workspaceId" => security.WorkspaceId,
            "roles" => security.Roles is { Count: > 0 } ? string.Join(",", security.Roles) : null,
            _ => null
        };

    private static object? ResolveVariablePath(string path, MicroflowActionExecutionContext context)
    {
        var dotIndex = path.IndexOf('.', StringComparison.Ordinal);
        var varName = dotIndex >= 0 ? path[..dotIndex] : path;
        var subPath = dotIndex >= 0 ? path[(dotIndex + 1)..] : null;

        if (!context.VariableStore.TryGet(varName, out var variable) || variable is null)
        {
            return null;
        }

        var rawJson = variable.RawValueJson;
        if (string.IsNullOrWhiteSpace(rawJson) || rawJson == "null")
        {
            return null;
        }

        if (subPath is null)
        {
            return ExtractScalarFromJson(rawJson);
        }

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            return ExtractNestedValue(doc.RootElement, subPath);
        }
        catch
        {
            return null;
        }
    }

    private static object? ExtractNestedValue(JsonElement element, string path)
    {
        var parts = path.Split('.');
        var current = element;
        foreach (var part in parts)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(part, out current))
            {
                return null;
            }
        }

        return ExtractScalarFromJson(current.GetRawText());
    }

    private static object? ExtractScalarFromJson(string rawJson)
    {
        if (rawJson == "null")
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            return doc.RootElement.ValueKind switch
            {
                JsonValueKind.String => doc.RootElement.GetString(),
                JsonValueKind.Number when doc.RootElement.TryGetInt64(out var l) => l,
                JsonValueKind.Number => doc.RootElement.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => doc.RootElement.GetRawText()
            };
        }
        catch
        {
            return rawJson;
        }
    }

    private static bool IsMysqlStyleDriver(string driverCode)
        => string.Equals(driverCode, "MySql", StringComparison.OrdinalIgnoreCase)
        || string.Equals(driverCode, "mysql", StringComparison.OrdinalIgnoreCase);

    private static bool IsInsideStringLiteral(string sql, int position)
    {
        var singleQuoteCount = 0;
        for (var i = 0; i < position; i++)
        {
            if (sql[i] == '\'')
            {
                singleQuoteCount++;
            }
        }

        return singleQuoteCount % 2 != 0;
    }
}
