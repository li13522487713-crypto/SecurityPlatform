using System.Text.RegularExpressions;
using Atlas.Domain.DynamicTables.Enums;

namespace Atlas.Infrastructure.DynamicTables;

/// <summary>
/// 校验迁移脚本内容，仅允许 ALTER TABLE ... ADD COLUMN 语句，防止任意 SQL 注入。
/// </summary>
internal static class MigrationScriptValidator
{
    /// <summary>
    /// 允许的列类型（SQLite 常用）。
    /// </summary>
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "INTEGER",
        "TEXT",
        "REAL",
        "BLOB",
        "NUMERIC"
    };

    /// <summary>
    /// 危险关键字黑名单，出现即拒绝。不含 ALTER（白名单允许 ALTER TABLE ADD COLUMN）。
    /// </summary>
    private static readonly string[] DangerousKeywords =
    [
        "DROP", "DELETE", "TRUNCATE", "INSERT", "UPDATE", "SELECT",
        "CREATE", "ATTACH", "DETACH", "VACUUM", "COPY", "PRAGMA",
        "REPLACE", "EXEC", "EXECUTE", "GRANT", "REVOKE"
    ];

    /// <summary>
    /// ALTER TABLE ... ADD COLUMN 白名单模式。
    /// 支持 SQLite/PostgreSQL 双引号、MySQL 反引号、SQL Server 方括号。
    /// </summary>
    private static readonly Regex AlterAddColumnPattern = new(
        @"^\s*ALTER\s+TABLE\s+(?:""([^""]+)""|`([^`]+)`|\[([^\]]+)\])\s+ADD\s+COLUMN\s+(?:""([^""]+)""|`([^`]+)`|\[([^\]]+)\])\s+(\w+)(?:\([^)]+\))?\s*(NOT\s+NULL)?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// 校验迁移脚本，仅允许针对指定表的 ALTER TABLE ADD COLUMN 语句。
    /// </summary>
    /// <param name="script">待执行的 SQL 脚本。</param>
    /// <param name="expectedTableKey">迁移记录关联的表标识，脚本中的表名必须与此一致。</param>
    /// <param name="dbType">数据库类型，用于确定标识符引用风格（当前主要用于黑名单校验）。</param>
    /// <returns>校验通过返回 null，否则返回错误信息。</returns>
    public static string? Validate(string script, string expectedTableKey, DynamicDbType dbType)
    {
        if (string.IsNullOrWhiteSpace(script))
        {
            return "迁移脚本不能为空。";
        }

        var normalized = script.AsSpan().Trim();
        if (ContainsDangerousKeyword(normalized))
        {
            return "迁移脚本包含不允许的 SQL 关键字，仅支持 ALTER TABLE ... ADD COLUMN 结构变更。";
        }

        var statements = SplitStatements(script);
        for (var i = 0; i < statements.Length; i++)
        {
            var stmt = statements[i].Trim();
            if (string.IsNullOrWhiteSpace(stmt))
            {
                continue;
            }

            if (IsCommentOnly(stmt))
            {
                continue;
            }

            var error = ValidateStatement(stmt, expectedTableKey);
            if (error is not null)
            {
                return $"第 {i + 1} 条语句校验失败：{error}";
            }
        }

        return null;
    }

    private static bool ContainsDangerousKeyword(ReadOnlySpan<char> script)
    {
        var upper = script.ToString().ToUpperInvariant();
        foreach (var kw in DangerousKeywords)
        {
            if (upper.Contains(kw, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string[] SplitStatements(string script)
    {
        return script.Split(';', StringSplitOptions.RemoveEmptyEntries);
    }

    private static bool IsCommentOnly(string stmt)
    {
        var lines = stmt.Split('\n', '\r');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                continue;
            }

            if (!trimmed.StartsWith("--", StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static string? ValidateStatement(string stmt, string expectedTableKey)
    {
        var match = AlterAddColumnPattern.Match(stmt);
        if (!match.Success)
        {
            return "仅允许 ALTER TABLE <表名> ADD COLUMN <列名> <类型> [NOT NULL] 格式的语句。";
        }

        var tableName = match.Groups[1].Success ? match.Groups[1].Value
            : match.Groups[2].Success ? match.Groups[2].Value
            : match.Groups[3].Value;

        if (!string.Equals(tableName, expectedTableKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"表名 '{tableName}' 与迁移目标表 '{expectedTableKey}' 不一致。";
        }

        var typeName = match.Groups[7].Value;
        if (!AllowedTypes.Contains(typeName) && !typeName.StartsWith("NUMERIC", StringComparison.OrdinalIgnoreCase))
        {
            return $"列类型 '{typeName}' 不在允许列表中（INTEGER/TEXT/REAL/BLOB/NUMERIC）。";
        }

        return null;
    }
}
