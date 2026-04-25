using System.Text;
using System.Text.RegularExpressions;
using Atlas.Application.AiPlatform.Abstractions;

namespace Atlas.Infrastructure.Services.DatabaseStructure;

public sealed class SqlSafetyValidator : ISqlSafetyValidator
{
    private static readonly Regex ForbiddenKeywords = new(
        @"\b(DROP|DELETE|UPDATE|INSERT|ALTER|TRUNCATE|EXEC|EXECUTE|MERGE|GRANT|REVOKE|CREATE\s+USER|CREATE\s+DATABASE|ATTACH|DETACH)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public void ValidateCreateTable(string sql)
    {
        var sanitized = NormalizeSingleStatement(sql);
        if (!Regex.IsMatch(sanitized, @"^\s*CREATE\s+TABLE\b", RegexOptions.IgnoreCase))
        {
            throw new SqlSafetyException("Only CREATE TABLE statements are allowed.");
        }

        ValidateNoForbiddenKeywords(sanitized, allowCreateTable: true, allowCreateView: false);
    }

    public void ValidateCreateView(string sql)
    {
        var sanitized = NormalizeSingleStatement(sql);
        if (Regex.IsMatch(sanitized, @"^\s*CREATE\s+(OR\s+REPLACE\s+)?VIEW\b", RegexOptions.IgnoreCase))
        {
            ValidateNoForbiddenKeywords(sanitized, allowCreateTable: false, allowCreateView: true);
            if (!Regex.IsMatch(sanitized, @"\bAS\s+(SELECT|WITH)\b", RegexOptions.IgnoreCase))
            {
                throw new SqlSafetyException("CREATE VIEW must be based on a SELECT statement.");
            }

            return;
        }

        ValidateSelectOnly(sanitized);
    }

    public void ValidateSelectOnly(string sql)
    {
        var sanitized = NormalizeSingleStatement(sql);
        if (!Regex.IsMatch(sanitized, @"^\s*(SELECT|WITH)\b", RegexOptions.IgnoreCase))
        {
            throw new SqlSafetyException("Only SELECT statements are allowed.");
        }

        ValidateNoForbiddenKeywords(sanitized, allowCreateTable: false, allowCreateView: false);
    }

    private static void ValidateNoForbiddenKeywords(string sql, bool allowCreateTable, bool allowCreateView)
    {
        var withoutAllowedCreate = sql;
        if (allowCreateTable)
        {
            withoutAllowedCreate = Regex.Replace(withoutAllowedCreate, @"^\s*CREATE\s+TABLE\b", string.Empty, RegexOptions.IgnoreCase);
        }

        if (allowCreateView)
        {
            withoutAllowedCreate = Regex.Replace(withoutAllowedCreate, @"^\s*CREATE\s+(OR\s+REPLACE\s+)?VIEW\b", string.Empty, RegexOptions.IgnoreCase);
        }

        if (ForbiddenKeywords.IsMatch(withoutAllowedCreate))
        {
            throw new SqlSafetyException("Dangerous SQL keyword detected.");
        }
    }

    private static string NormalizeSingleStatement(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new SqlSafetyException("SQL statement cannot be empty.");
        }

        var stripped = StripComments(sql).Trim();
        var statements = SplitStatements(stripped).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        if (statements.Count != 1)
        {
            throw new SqlSafetyException("Multiple SQL statements are not allowed.");
        }

        return statements[0].Trim();
    }

    private static string StripComments(string sql)
    {
        var builder = new StringBuilder(sql.Length);
        var inSingle = false;
        var inDouble = false;
        for (var i = 0; i < sql.Length; i++)
        {
            var ch = sql[i];
            var next = i + 1 < sql.Length ? sql[i + 1] : '\0';
            if (!inSingle && !inDouble && ch == '-' && next == '-')
            {
                while (i < sql.Length && sql[i] != '\n')
                {
                    i++;
                }
                builder.Append('\n');
                continue;
            }

            if (!inSingle && !inDouble && ch == '/' && next == '*')
            {
                i += 2;
                while (i + 1 < sql.Length && !(sql[i] == '*' && sql[i + 1] == '/'))
                {
                    i++;
                }
                i++;
                builder.Append(' ');
                continue;
            }

            if (ch == '\'' && !inDouble)
            {
                inSingle = !inSingle;
            }
            else if (ch == '"' && !inSingle)
            {
                inDouble = !inDouble;
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }

    private static IEnumerable<string> SplitStatements(string sql)
    {
        var builder = new StringBuilder();
        var inSingle = false;
        var inDouble = false;
        for (var i = 0; i < sql.Length; i++)
        {
            var ch = sql[i];
            if (ch == '\'' && !inDouble)
            {
                inSingle = !inSingle;
            }
            else if (ch == '"' && !inSingle)
            {
                inDouble = !inDouble;
            }

            if (ch == ';' && !inSingle && !inDouble)
            {
                yield return builder.ToString();
                builder.Clear();
                continue;
            }

            builder.Append(ch);
        }

        yield return builder.ToString();
    }
}
