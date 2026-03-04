using System.Text;
using System.Text.RegularExpressions;
using Atlas.Domain.DynamicTables.Enums;

namespace Atlas.Infrastructure.DynamicTables;

/// <summary>
/// 校验迁移脚本内容，仅允许 ALTER TABLE ... ADD COLUMN 语句，防止任意 SQL 注入。
/// 安全边界：用户输入的 UpScript 必须通过白名单校验后才能执行，禁止 DROP/SELECT/INSERT 等任意 SQL。
/// </summary>
internal static class MigrationScriptValidator
{
    /// <summary>
    /// 脚本最大长度，防止 DoS。
    /// </summary>
    private const int MaxScriptLength = 65536;

    /// <summary>
    /// 允许的列类型。覆盖 SQLite、SqlServer、MySql、PostgreSql 的常用类型，
    /// 与 DynamicSqlBuilder.MapToSqlType 及各库 ALTER ADD COLUMN 语法兼容。
    /// </summary>
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        // SQLite
        "INTEGER",
        "TEXT",
        "REAL",
        "BLOB",
        "NUMERIC",
        // SqlServer / MySql / PostgreSQL 常用
        "INT",
        "BIGINT",
        "SMALLINT",
        "TINYINT",
        "VARCHAR",
        "NVARCHAR",
        "CHAR",
        "NCHAR",
        "DECIMAL",
        "FLOAT",
        "DOUBLE",
        "BIT",
        "BOOLEAN",
        "DATETIME",
        "DATE",
        "TIMESTAMP",
        "TIME",
        "DATETIME2",
        "DATETIMEOFFSET"
    };

    /// <summary>
    /// 危险关键字黑名单，出现即拒绝。不含 ALTER（白名单允许 ALTER TABLE ADD COLUMN）。
    /// </summary>
    private static readonly string[] DangerousKeywords =
    [
        "DROP", "DELETE", "TRUNCATE", "INSERT", "UPDATE", "SELECT",
        "CREATE", "ATTACH", "DETACH", "VACUUM", "COPY", "PRAGMA",
        "REPLACE", "EXEC", "EXECUTE", "GRANT", "REVOKE",
        "ANALYZE", "EXPLAIN", "WITH", "UNION"
    ];

    /// <summary>
    /// ALTER TABLE ... ADD COLUMN 白名单模式。
    /// 支持 SQLite/PostgreSQL 双引号、MySQL 反引号、SQL Server 方括号，以及未加引号的简单标识符
    ///（与 DynamicSqlBuilder.Quote 输出及手动编写的 SQL 兼容）。
    /// </summary>
    private static readonly Regex AlterAddColumnPattern = new(
        @"^\s*ALTER\s+TABLE\s+(?:""([^""]+)""|`([^`]+)`|\[([^\]]+)\]|([a-zA-Z_][a-zA-Z0-9_]*))\s+ADD\s+COLUMN\s+(?:""([^""]+)""|`([^`]+)`|\[([^\]]+)\]|([a-zA-Z_][a-zA-Z0-9_]*))\s+(\w+)(?:\([^)]+\))?\s*(NOT\s+NULL)?\s*$",
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

        if (script.Length > MaxScriptLength)
        {
            return $"迁移脚本长度超过限制（最大 {MaxScriptLength} 字符）。";
        }

        if (ContainsDangerousKeyword(script))
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

            // 去除尾随注释后再校验，避免 "ALTER TABLE ... ADD COLUMN x -- comment" 被误拒
            var stmtWithoutTrailingComments = StripSqlComments(stmt).Trim();
            if (string.IsNullOrWhiteSpace(stmtWithoutTrailingComments))
            {
                continue;
            }

            var error = ValidateStatement(stmtWithoutTrailingComments, expectedTableKey);
            if (error is not null)
            {
                return $"第 {i + 1} 条语句校验失败：{error}";
            }
        }

        return null;
    }

    /// <summary>
    /// 检测脚本中是否包含危险关键字。仅匹配整词（非标识符子串），避免 created_at、user_grants 等误报。
    /// 使用 \b 词边界，确保 CREATE 不匹配 created_at，GRANT 不匹配 user_grants。
    /// 仅检查可执行 SQL 部分，忽略注释内容，避免 -- TODO: DROP COLUMN 等注释导致误报。
    /// 在检查前将引号/方括号/反引号内的标识符替换为占位符，避免用户定义的表名或列名（如 select、copy、update）
    /// 被误判为危险关键字。
    /// </summary>
    private static bool ContainsDangerousKeyword(string script)
    {
        var withoutComments = StripSqlComments(script);
        var masked = MaskQuotedIdentifiers(withoutComments);
        var upper = masked.ToUpperInvariant();
        foreach (var kw in DangerousKeywords)
        {
            var pattern = $@"\b{Regex.Escape(kw)}\b";
            if (Regex.IsMatch(upper, pattern))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 将 SQL 中的引用标识符（双引号、方括号、反引号）替换为占位符，避免用户定义的表名/列名
    /// 与危险关键字重合时产生误报（如字段名 select、表名 copy）。
    /// </summary>
    private static string MaskQuotedIdentifiers(string sql)
    {
        return Regex.Replace(sql, @"""([^""]*)""|\[([^\]]+)\]|`([^`]+)`", "X");
    }

    /// <summary>
    /// 移除 SQL 注释（-- 行注释与 /* */ 块注释），用于危险关键字检查前的预处理。
    /// </summary>
    private static string StripSqlComments(string script)
    {
        var sb = new StringBuilder(script.Length);
        var i = 0;
        while (i < script.Length)
        {
            if (i + 1 < script.Length && script[i] == '/' && script[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < script.Length && !(script[i] == '*' && script[i + 1] == '/'))
                {
                    i++;
                }
                if (i + 1 < script.Length)
                {
                    i += 2;
                }
                continue;
            }

            if (i + 1 < script.Length && script[i] == '-' && script[i + 1] == '-')
            {
                while (i < script.Length && script[i] != '\n')
                {
                    i++;
                }
                continue;
            }

            sb.Append(script[i]);
            i++;
        }
        return sb.ToString();
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
            : match.Groups[3].Success ? match.Groups[3].Value
            : match.Groups[4].Value;

        if (!string.Equals(tableName, expectedTableKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"表名 '{tableName}' 与迁移目标表 '{expectedTableKey}' 不一致。";
        }

        var typeName = match.Groups[9].Value;
        if (!AllowedTypes.Contains(typeName) && !typeName.StartsWith("NUMERIC", StringComparison.OrdinalIgnoreCase))
        {
            return $"列类型 '{typeName}' 不在允许列表中，仅支持 SQLite/SqlServer/MySql/PostgreSql 的常用类型。";
        }

        return null;
    }
}
