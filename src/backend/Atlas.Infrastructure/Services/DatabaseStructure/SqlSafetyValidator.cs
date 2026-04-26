using System.Text;
using System.Text.RegularExpressions;
using Atlas.Application.AiPlatform.Abstractions;

namespace Atlas.Infrastructure.Services.DatabaseStructure;

public sealed partial class SqlSafetyValidator : ISqlSafetyValidator
{
    private static readonly HashSet<string> ForbiddenTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "DROP",
        "DELETE",
        "UPDATE",
        "INSERT",
        "ALTER",
        "TRUNCATE",
        "EXEC",
        "EXECUTE",
        "MERGE",
        "GRANT",
        "REVOKE",
        "ATTACH",
        "DETACH"
    };

    private static readonly string[] ForbiddenPhrases =
    [
        "CREATE DATABASE",
        "CREATE USER",
        "INTO OUTFILE",
        "COPY TO PROGRAM"
    ];

    private static readonly string[] ForbiddenFunctions =
    [
        "LOAD_FILE",
        "XP_CMDSHELL"
    ];

    public void ValidateCreateTable(string sql)
    {
        var statement = SingleStatement(sql);
        if (!StartsWithKeywordSequence(statement, "CREATE", "TABLE"))
        {
            throw new SqlSafetyException("SQL_CREATE_TABLE_REQUIRED", "SQL 建表失败：这里只允许执行一条 CREATE TABLE 语句。");
        }

        EnsureNoForbidden(statement, allowedLeadingCreate: "TABLE");
    }

    public void ValidateCreateView(string sql)
    {
        var statement = SingleStatement(sql);
        if (!StartsWithCreateView(statement))
        {
            throw new SqlSafetyException("SQL_CREATE_VIEW_REQUIRED", "SQL 建视图失败：这里只允许执行 CREATE VIEW 或 CREATE OR REPLACE VIEW 语句。");
        }

        EnsureNoForbidden(statement, allowedLeadingCreate: "VIEW");
        var tokens = TokenizeExecutableText(statement).ToList();
        var asIndex = tokens.FindIndex(token => string.Equals(token, "AS", StringComparison.OrdinalIgnoreCase));
        if (asIndex < 0 || tokens.Skip(asIndex + 1).All(token => !string.Equals(token, "SELECT", StringComparison.OrdinalIgnoreCase) && !string.Equals(token, "WITH", StringComparison.OrdinalIgnoreCase)))
        {
            throw new SqlSafetyException("SQL_CREATE_VIEW_SELECT_REQUIRED", "SQL 建视图失败：CREATE VIEW 必须包含 AS SELECT 查询。");
        }
    }

    public void ValidateSelectOnly(string sql)
    {
        var statement = SingleStatement(sql);
        var tokens = TokenizeExecutableText(statement).ToList();
        if (tokens.Count == 0 || (!string.Equals(tokens[0], "SELECT", StringComparison.OrdinalIgnoreCase) && !string.Equals(tokens[0], "WITH", StringComparison.OrdinalIgnoreCase)))
        {
            throw new SqlSafetyException("SQL_SELECT_REQUIRED", "SQL 预览失败：这里只允许 SELECT 或 WITH 查询。");
        }

        EnsureNoForbidden(statement, allowedLeadingCreate: null);
    }

    public void ValidateSqlEditorExecute(string sql)
    {
        var statement = SingleStatement(sql);
        var tokens = TokenizeExecutableText(statement).ToList();
        if (tokens.Count == 0)
        {
            throw new SqlSafetyException("SQL_EMPTY", "SQL 执行失败：SQL 内容不能为空。");
        }

        if (string.Equals(tokens[0], "SELECT", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tokens[0], "WITH", StringComparison.OrdinalIgnoreCase))
        {
            EnsureNoForbidden(statement, allowedLeadingCreate: null);
            return;
        }

        if (tokens.Count >= 2 &&
            string.Equals(tokens[0], "INSERT", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(tokens[1], "INTO", StringComparison.OrdinalIgnoreCase))
        {
            EnsureNoForbidden(statement, allowedLeadingCreate: null, allowedLeadingInsert: true);
            return;
        }

        throw new SqlSafetyException("SQL_EDITOR_STATEMENT_FORBIDDEN", "SQL 执行失败：SQL 编辑器只允许 SELECT/WITH 查询或 INSERT INTO 插入数据。建表请使用“SQL 建表”，建视图请使用“新建视图”。");
    }

    public IReadOnlyList<string> SplitStatementsSafely(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return [];
        }

        var result = new List<string>();
        var builder = new StringBuilder();
        var state = ScannerState.Normal;
        for (var i = 0; i < sql.Length; i++)
        {
            var ch = sql[i];
            var next = i + 1 < sql.Length ? sql[i + 1] : '\0';

            if (state == ScannerState.Normal)
            {
                if (ch == '/' && next == '*')
                {
                    if (i + 2 < sql.Length && sql[i + 2] == '!')
                    {
                        throw new SqlSafetyException("SQL_MYSQL_VERSION_COMMENT_FORBIDDEN", "MySQL executable comments are not allowed.");
                    }

                    state = ScannerState.BlockComment;
                    builder.Append(' ');
                    i++;
                    continue;
                }

                if (ch == '-' && next == '-')
                {
                    state = ScannerState.LineComment;
                    builder.Append(' ');
                    i++;
                    continue;
                }

                if (ch == '\'')
                {
                    state = ScannerState.SingleQuoted;
                    builder.Append(' ');
                    continue;
                }

                if (ch == '"')
                {
                    state = ScannerState.DoubleQuoted;
                    builder.Append(' ');
                    continue;
                }

                if (ch == '`')
                {
                    state = ScannerState.BacktickQuoted;
                    builder.Append(' ');
                    continue;
                }

                if (ch == '[')
                {
                    state = ScannerState.BracketQuoted;
                    builder.Append(' ');
                    continue;
                }

                if (ch == ';')
                {
                    result.Add(builder.ToString());
                    builder.Clear();
                    continue;
                }

                builder.Append(ch);
                continue;
            }

            if (state == ScannerState.LineComment)
            {
                if (ch is '\r' or '\n')
                {
                    state = ScannerState.Normal;
                    builder.Append(ch);
                }

                continue;
            }

            if (state == ScannerState.BlockComment)
            {
                if (ch == '*' && next == '/')
                {
                    state = ScannerState.Normal;
                    i++;
                }

                continue;
            }

            if (state == ScannerState.SingleQuoted)
            {
                if (ch == '\\' && next != '\0')
                {
                    i++;
                    continue;
                }

                if (ch == '\'' && next == '\'')
                {
                    i++;
                    continue;
                }

                if (ch == '\'')
                {
                    state = ScannerState.Normal;
                }

                continue;
            }

            if (state == ScannerState.DoubleQuoted)
            {
                if (ch == '\\' && next != '\0')
                {
                    i++;
                    continue;
                }

                if (ch == '"' && next == '"')
                {
                    i++;
                    continue;
                }

                if (ch == '"')
                {
                    state = ScannerState.Normal;
                }

                continue;
            }

            if (state == ScannerState.BacktickQuoted)
            {
                if (ch == '`' && next == '`')
                {
                    i++;
                    continue;
                }

                if (ch == '`')
                {
                    state = ScannerState.Normal;
                }

                continue;
            }

            if (state == ScannerState.BracketQuoted)
            {
                if (ch == ']' && next == ']')
                {
                    i++;
                    continue;
                }

                if (ch == ']')
                {
                    state = ScannerState.Normal;
                }
            }
        }

        if (state is ScannerState.SingleQuoted or ScannerState.DoubleQuoted or ScannerState.BacktickQuoted or ScannerState.BracketQuoted or ScannerState.BlockComment)
        {
            throw new SqlSafetyException("SQL_UNCLOSED_LITERAL_OR_COMMENT", "SQL 校验失败：字符串、标识符或注释没有闭合，请检查引号、反引号、中括号或 /* */ 注释。");
        }

        result.Add(builder.ToString());
        return result.Select(item => item.Trim()).Where(item => item.Length > 0).ToList();
    }

    public bool ContainsForbiddenKeyword(string sql)
    {
        try
        {
            foreach (var statement in SplitStatementsSafely(sql))
            {
                EnsureNoForbidden(statement, allowedLeadingCreate: null);
            }

            return false;
        }
        catch (SqlSafetyException)
        {
            return true;
        }
    }

    private static bool StartsWithCreateView(string statement)
        => StartsWithKeywordSequence(statement, "CREATE", "VIEW") ||
           StartsWithKeywordSequence(statement, "CREATE", "OR", "REPLACE", "VIEW");

    private static bool StartsWithKeywordSequence(string statement, params string[] expected)
    {
        var tokens = TokenizeExecutableText(statement).Take(expected.Length).ToList();
        return tokens.Count == expected.Length &&
               tokens.Zip(expected, (actual, wanted) => string.Equals(actual, wanted, StringComparison.OrdinalIgnoreCase)).All(BooleanIdentity);
    }

    private static bool BooleanIdentity(bool value) => value;

    private string SingleStatement(string sql)
    {
        var statements = SplitStatementsSafely(sql);
        if (statements.Count == 0)
        {
            throw new SqlSafetyException("SQL_EMPTY", "SQL 校验失败：SQL 内容不能为空。");
        }

        if (statements.Count != 1)
        {
            throw new SqlSafetyException("SQL_MULTIPLE_STATEMENTS_FORBIDDEN", "SQL 校验失败：一次只能执行一条 SQL，请拆成多次执行。");
        }

        return statements[0];
    }

    private static void EnsureNoForbidden(string statement, string? allowedLeadingCreate, bool allowedLeadingInsert = false)
    {
        var tokens = TokenizeExecutableText(statement).ToList();
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (ForbiddenTokens.Contains(token))
            {
                if (i == 0 &&
                    allowedLeadingInsert &&
                    string.Equals(token, "INSERT", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                throw new SqlSafetyException("SQL_FORBIDDEN_KEYWORD", $"SQL 校验失败：检测到不允许的关键字 {token}。");
            }

            if (IsForbiddenFunction(token))
            {
                throw new SqlSafetyException("SQL_FORBIDDEN_FUNCTION", $"SQL 校验失败：检测到不允许的函数 {token}。");
            }

            if (string.Equals(token, "CREATE", StringComparison.OrdinalIgnoreCase))
            {
                var next = i + 1 < tokens.Count ? tokens[i + 1] : string.Empty;
                if (i == 0 && allowedLeadingCreate is not null && string.Equals(next, allowedLeadingCreate, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (i == 0 && string.Equals(next, "OR", StringComparison.OrdinalIgnoreCase) && allowedLeadingCreate is "VIEW")
                {
                    continue;
                }

                throw new SqlSafetyException("SQL_FORBIDDEN_CREATE", "SQL 校验失败：当前入口不允许执行该 CREATE 语句。建表请使用“SQL 建表”，建视图请使用“新建视图”。");
            }
        }

        var executable = string.Join(' ', tokens).ToUpperInvariant();
        foreach (var phrase in ForbiddenPhrases)
        {
            if (executable.Contains(phrase, StringComparison.Ordinal))
            {
                throw new SqlSafetyException("SQL 校验失败：检测到不允许的高危 SQL 片段。", "SQL_FORBIDDEN_PHRASE");
            }
        }

        if (ContainsCopyToProgram(tokens))
        {
            throw new SqlSafetyException("SQL 校验失败：检测到不允许的高危 SQL 片段。", "SQL_FORBIDDEN_PHRASE");
        }
    }

    private static bool IsForbiddenFunction(string token)
        => ForbiddenFunctions.Any(function => string.Equals(function, token, StringComparison.OrdinalIgnoreCase));

    private static bool ContainsCopyToProgram(IReadOnlyList<string> tokens)
    {
        for (var i = 0; i < tokens.Count; i++)
        {
            if (!string.Equals(tokens[i], "COPY", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            for (var j = i + 1; j + 1 < tokens.Count; j++)
            {
                if (string.Equals(tokens[j], "TO", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(tokens[j + 1], "PROGRAM", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static IEnumerable<string> TokenizeExecutableText(string statement)
    {
        foreach (Match match in TokenPattern().Matches(statement))
        {
            yield return match.Value;
        }
    }

    [GeneratedRegex(@"[A-Za-z_][A-Za-z0-9_]*")]
    private static partial Regex TokenPattern();

    private enum ScannerState
    {
        Normal,
        SingleQuoted,
        DoubleQuoted,
        BacktickQuoted,
        BracketQuoted,
        LineComment,
        BlockComment
    }
}
