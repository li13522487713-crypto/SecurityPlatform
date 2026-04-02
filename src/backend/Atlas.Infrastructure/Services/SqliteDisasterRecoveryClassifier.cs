using Microsoft.Data.Sqlite;

namespace Atlas.Infrastructure.Services;

internal enum SqliteFailureKind
{
    None = 0,
    DiskImageMalformed = 1,
    MalformedSchema = 2,
    DiskFull = 3,
    Other = 4
}

internal static class SqliteDisasterRecoveryClassifier
{
    public static SqliteFailureKind Classify(Exception ex)
    {
        if (!TryFindSqliteException(ex, out var sqliteException))
        {
            return SqliteFailureKind.None;
        }

        return Classify(sqliteException);
    }

    public static SqliteFailureKind Classify(SqliteException ex)
    {
        var message = ex.Message ?? string.Empty;
        if (Contains(message, "database disk image is malformed"))
        {
            return SqliteFailureKind.DiskImageMalformed;
        }

        if (Contains(message, "malformed database schema"))
        {
            return SqliteFailureKind.MalformedSchema;
        }

        if (Contains(message, "database or disk is full"))
        {
            return SqliteFailureKind.DiskFull;
        }

        return SqliteFailureKind.Other;
    }

    public static bool IsCorruption(Exception ex)
    {
        return Classify(ex) == SqliteFailureKind.DiskImageMalformed;
    }

    public static bool TryFindSqliteException(Exception ex, out SqliteException sqliteException)
    {
        if (ex is AggregateException aggregateException && aggregateException.InnerExceptions.Count == 1)
        {
            ex = aggregateException.InnerExceptions[0];
        }

        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is SqliteException se)
            {
                sqliteException = se;
                return true;
            }
        }

        sqliteException = null!;
        return false;
    }

    private static bool Contains(string message, string keyword)
    {
        return message.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }
}
