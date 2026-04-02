using Atlas.Application.BatchProcess.Abstractions;
using Atlas.Application.BatchProcess.Models;
using Atlas.Core.Exceptions;
using SqlSugar;
using System.Text.RegularExpressions;

namespace Atlas.Infrastructure.BatchProcess.Scanning;

/// <summary>
/// 基于 keyset（游标）分页扫描，避免 OFFSET 带来的深分页性能退化。
/// </summary>
public sealed partial class KeysetScanner : IKeysetScanner
{
    private readonly ISqlSugarClient _db;

    public KeysetScanner(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<KeysetScanResult> ScanAsync(KeysetScanRequest request, CancellationToken cancellationToken)
    {
        var sql = BuildScanSql(request);
        var keys = await _db.Ado.SqlQueryAsync<string>(sql, cancellationToken);

        var hasMore = keys.Count > request.PageSize;
        if (hasMore)
        {
            keys = keys.Take(request.PageSize).ToList();
        }

        return new KeysetScanResult
        {
            Keys = keys,
            LastKey = keys.Count > 0 ? keys[^1] : null,
            HasMore = hasMore
        };
    }

    private static string BuildScanSql(KeysetScanRequest request)
    {
        var table = SanitizeIdentifier(request.TableName);
        var keyCol = SanitizeIdentifier(request.KeyColumn);
        var limit = request.PageSize + 1;

        var whereClause = string.IsNullOrEmpty(request.AfterKey)
            ? string.Empty
            : $" WHERE {keyCol} > '{EscapeSqlValue(request.AfterKey)}'";

        if (!string.IsNullOrEmpty(request.FilterExpression))
        {
            var filter = SanitizeFilterExpression(request.FilterExpression);
            if (!string.IsNullOrEmpty(filter))
            {
                whereClause = string.IsNullOrEmpty(whereClause)
                    ? $" WHERE ({filter})"
                    : $"{whereClause} AND ({filter})";
            }
        }

        return $"SELECT CAST({keyCol} AS TEXT) FROM {table}{whereClause} ORDER BY {keyCol} ASC LIMIT {limit}";
    }

    private static string SanitizeIdentifier(string identifier)
    {
        return identifier.Replace("\"", "").Replace("'", "").Replace(";", "");
    }

    private static string EscapeSqlValue(string value)
    {
        return value.Replace("'", "''");
    }

    private static string SanitizeFilterExpression(string expression)
    {
        var trimmed = expression.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        // 拒绝明显可导致 SQL 注入的语法和关键字，避免拼接执行任意 SQL。
        if (trimmed.Contains(';')
            || trimmed.Contains("--", StringComparison.Ordinal)
            || trimmed.Contains("/*", StringComparison.Ordinal)
            || trimmed.Contains("*/", StringComparison.Ordinal))
        {
            throw new BusinessException("VALIDATION_ERROR", "过滤表达式包含非法 SQL 片段");
        }

        if (DangerousSqlKeywordRegex().IsMatch(trimmed) || !AllowedFilterCharsRegex().IsMatch(trimmed))
        {
            throw new BusinessException("VALIDATION_ERROR", "过滤表达式包含不允许的关键字或字符");
        }

        return trimmed;
    }

    [GeneratedRegex(@"\b(select|insert|update|delete|drop|alter|create|truncate|exec|execute|attach|detach|pragma|union)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DangerousSqlKeywordRegex();

    [GeneratedRegex(@"^[A-Za-z0-9_\s\.\(\),'""><=!+\-/*%:]+$", RegexOptions.CultureInvariant)]
    private static partial Regex AllowedFilterCharsRegex();
}
