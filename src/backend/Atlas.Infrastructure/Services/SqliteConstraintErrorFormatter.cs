using Microsoft.Data.Sqlite;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 将 SQLite / SqlSugar 包装的约束异常整理为可定位、可操作的摘要文案。
/// </summary>
internal static class SqliteConstraintErrorFormatter
{
    private const string NotNullHint =
        "提示：多为目标库列 NOT NULL 与当前实体/数据不一致。请确认已执行迁移「结构自修复」阶段；若为历史库，可重试迁移任务。";

    private const string DiskMalformedHint =
        "提示：检测到 SQLite 物理损坏，系统将尝试自动重建空应用库并重跑迁移；请查看迁移任务进度与 schemaRepairLog。";

    private const string MalformedSchemaHint =
        "提示：检测到 SQLite catalog/schema 脏元数据；系统会先执行 sqlite_master 自动清理后继续迁移。";

    private const string DiskFullHint =
        "提示：磁盘空间不足，不触发自动重建。请先清理磁盘空间后重试迁移。";

    private const string GenericSqliteHint =
        "提示：请查看 SQLite 错误码与消息，核对目标应用库表结构及外键/唯一约束。";

    public static string Format(Exception ex)
    {
        if (ex is AggregateException agg && agg.InnerExceptions.Count == 1)
        {
            ex = agg.InnerExceptions[0];
        }

        var chain = new List<Exception>();
        for (var e = ex; e is not null; e = e.InnerException)
        {
            chain.Add(e);
        }

        foreach (var e in chain)
        {
            if (e is SqliteException se)
            {
                return FormatSqliteException(se);
            }
        }

        return ex.Message;
    }

    private static string FormatSqliteException(SqliteException se)
    {
        var msg = se.Message?.Trim() ?? string.Empty;
        var kind = SqliteDisasterRecoveryClassifier.Classify(se);

        if (kind == SqliteFailureKind.DiskImageMalformed)
        {
            return $"{msg} (SQLite Error {se.SqliteErrorCode}) | {DiskMalformedHint}";
        }

        if (kind == SqliteFailureKind.MalformedSchema)
        {
            return $"{msg} (SQLite Error {se.SqliteErrorCode}) | {MalformedSchemaHint}";
        }

        if (kind == SqliteFailureKind.DiskFull)
        {
            return $"{msg} (SQLite Error {se.SqliteErrorCode}) | {DiskFullHint}";
        }

        if (msg.Contains("NOT NULL constraint failed", StringComparison.OrdinalIgnoreCase))
        {
            var detail = TryParseNotNullTarget(msg);
            return string.IsNullOrEmpty(detail)
                ? $"{msg} | {NotNullHint}"
                : $"{msg} | 列: {detail} | {NotNullHint}";
        }

        return $"{msg} (SQLite Error {se.SqliteErrorCode}) | {GenericSqliteHint}";
    }

    /// <summary>
    /// 解析 "NOT NULL constraint failed: AppRole.DeptIds" 形式。
    /// </summary>
    private static string? TryParseNotNullTarget(string message)
    {
        const string marker = "NOT NULL constraint failed:";
        var idx = message.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return null;
        }

        var tail = message[(idx + marker.Length)..].Trim();
        return string.IsNullOrEmpty(tail) ? null : tail.Trim(' ', '.');
    }
}
