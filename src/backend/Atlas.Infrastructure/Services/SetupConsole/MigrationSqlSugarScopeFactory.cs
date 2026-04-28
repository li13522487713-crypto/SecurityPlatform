using System.Security.Cryptography;
using System.Text;
using SqlSugar;

namespace Atlas.Infrastructure.Services.SetupConsole;

internal static class MigrationSqlSugarScopeFactory
{
    internal const string AiRowIdColumn = "atlas_row_id";
    internal const string AiDataJsonColumn = "atlas_data_json";
    internal const string AiOwnerUserIdColumn = "atlas_owner_user_id";
    internal const string AiCreatorUserIdColumn = "atlas_creator_user_id";
    internal const string AiChannelIdColumn = "atlas_channel_id";
    internal const string AiCreatedAtColumn = "atlas_created_at";
    internal const string AiUpdatedAtColumn = "atlas_updated_at";

    public static ISqlSugarClient Create(string connectionString, string dbType)
    {
        var resolvedDbType = DataSourceDriverRegistry.ResolveDbType(dbType);
        return new SqlSugarScope(new ConnectionConfig
        {
            ConnectionString = connectionString,
            DbType = resolvedDbType,
            IsAutoCloseConnection = true,
            ConfigureExternalServices = new ConfigureExternalServices
            {
                EntityService = (property, column) =>
                {
                    if (property.Name == nameof(Atlas.Core.Abstractions.TenantEntity.TenantId)
                        && property.PropertyType == typeof(Atlas.Core.Tenancy.TenantId))
                    {
                        column.IsIgnore = true;
                        return;
                    }

                    if (property.Name == "Id" && property.PropertyType == typeof(long))
                    {
                        column.IsPrimarykey = true;
                        column.IsIdentity = false;
                    }

                    if (column.DataType is "TEXT" or "text"
                        && property.PropertyType == typeof(string)
                        && property.GetCustomAttributes(typeof(SugarColumn), inherit: false).Length > 0)
                    {
                        column.DataType = resolvedDbType switch
                        {
                            DbType.MySql => "LONGTEXT",
                            DbType.SqlServer => "NVARCHAR(MAX)",
                            DbType.PostgreSQL or DbType.Kdbndp or DbType.Dm or DbType.Oscar => "TEXT",
                            DbType.Oracle => "CLOB",
                            _ => column.DataType
                        };
                    }
                }
            }
        });
    }

    public static string QuoteIdentifier(string dbType, string name)
    {
        var normalized = DataSourceDriverRegistry.NormalizeDriverCode(dbType);
        return normalized switch
        {
            "SqlServer" => $"[{name.Replace("]", "]]", StringComparison.Ordinal)}]",
            "MySql" => $"`{name.Replace("`", "``", StringComparison.Ordinal)}`",
            _ => $"\"{name.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
        };
    }

    public static string BuildPagedSelectSql(string dbType, string tableName, string keyColumn, int batchSize)
    {
        var quotedTable = QuoteIdentifier(dbType, tableName);
        var quotedKey = QuoteIdentifier(dbType, keyColumn);
        var normalized = DataSourceDriverRegistry.NormalizeDriverCode(dbType);

        return normalized switch
        {
            "SqlServer" => $"SELECT TOP ({batchSize}) * FROM {quotedTable} WHERE {quotedKey} > @lastMaxId ORDER BY {quotedKey} ASC;",
            "Oracle" => $"SELECT * FROM {quotedTable} WHERE {quotedKey} > @lastMaxId ORDER BY {quotedKey} ASC FETCH NEXT {batchSize} ROWS ONLY",
            _ => $"SELECT * FROM {quotedTable} WHERE {quotedKey} > @lastMaxId ORDER BY {quotedKey} ASC LIMIT {batchSize};"
        };
    }

    public static string BuildCountSql(string dbType, string tableName)
    {
        return $"SELECT COUNT(1) FROM {QuoteIdentifier(dbType, tableName)};";
    }

    public static string BuildDeleteAllSql(string dbType, string tableName)
    {
        return $"DELETE FROM {QuoteIdentifier(dbType, tableName)};";
    }

    public static string BuildTruncateSql(string dbType, string tableName)
    {
        return $"TRUNCATE TABLE {QuoteIdentifier(dbType, tableName)};";
    }

    public static string BuildCreateAiTableSql(string dbType, string tableName)
    {
        var table = QuoteIdentifier(dbType, tableName);
        var rowId = QuoteIdentifier(dbType, AiRowIdColumn);
        var dataJson = QuoteIdentifier(dbType, AiDataJsonColumn);
        var ownerUserId = QuoteIdentifier(dbType, AiOwnerUserIdColumn);
        var creatorUserId = QuoteIdentifier(dbType, AiCreatorUserIdColumn);
        var channelId = QuoteIdentifier(dbType, AiChannelIdColumn);
        var createdAt = QuoteIdentifier(dbType, AiCreatedAtColumn);
        var updatedAt = QuoteIdentifier(dbType, AiUpdatedAtColumn);
        var normalized = DataSourceDriverRegistry.NormalizeDriverCode(dbType);

        return normalized switch
        {
            "MySql" => $"""
                CREATE TABLE {table} (
                    {rowId} BIGINT NOT NULL,
                    {dataJson} LONGTEXT NOT NULL,
                    {ownerUserId} BIGINT NULL,
                    {creatorUserId} BIGINT NULL,
                    {channelId} VARCHAR(256) NULL,
                    {createdAt} DATETIME(6) NOT NULL,
                    {updatedAt} DATETIME(6) NULL,
                    PRIMARY KEY ({rowId})
                );
                """,
            "SqlServer" => $"""
                CREATE TABLE {table} (
                    {rowId} BIGINT NOT NULL PRIMARY KEY,
                    {dataJson} NVARCHAR(MAX) NOT NULL,
                    {ownerUserId} BIGINT NULL,
                    {creatorUserId} BIGINT NULL,
                    {channelId} NVARCHAR(256) NULL,
                    {createdAt} DATETIME2 NOT NULL,
                    {updatedAt} DATETIME2 NULL
                );
                """,
            "Oracle" => $"""
                CREATE TABLE {table} (
                    {rowId} NUMBER(19) NOT NULL,
                    {dataJson} CLOB NOT NULL,
                    {ownerUserId} NUMBER(19) NULL,
                    {creatorUserId} NUMBER(19) NULL,
                    {channelId} VARCHAR2(256) NULL,
                    {createdAt} TIMESTAMP NOT NULL,
                    {updatedAt} TIMESTAMP NULL,
                    CONSTRAINT {SafeConstraintName(tableName, "pk")} PRIMARY KEY ({rowId})
                )
                """,
            "SQLite" => $"""
                CREATE TABLE {table} (
                    {rowId} INTEGER PRIMARY KEY,
                    {dataJson} TEXT NOT NULL,
                    {ownerUserId} INTEGER NULL,
                    {creatorUserId} INTEGER NULL,
                    {channelId} TEXT NULL,
                    {createdAt} TEXT NOT NULL,
                    {updatedAt} TEXT NULL
                );
                """,
            _ => $"""
                CREATE TABLE {table} (
                    {rowId} BIGINT NOT NULL,
                    {dataJson} TEXT NOT NULL,
                    {ownerUserId} BIGINT NULL,
                    {creatorUserId} BIGINT NULL,
                    {channelId} VARCHAR(256) NULL,
                    {createdAt} TIMESTAMP NOT NULL,
                    {updatedAt} TIMESTAMP NULL,
                    PRIMARY KEY ({rowId})
                );
                """
        };
    }

    public static string BuildCreateAiIndexSql(string dbType, string tableName, string suffix, string columnName)
    {
        var indexName = QuoteIdentifier(dbType, SafeConstraintName(tableName, suffix));
        var table = QuoteIdentifier(dbType, tableName);
        var column = QuoteIdentifier(dbType, columnName);
        return $"CREATE INDEX {indexName} ON {table} ({column});";
    }

    private static string SafeConstraintName(string tableName, string suffix)
    {
        var normalized = tableName.Replace("-", "_", StringComparison.Ordinal);
        var raw = $"{normalized}_{suffix}";
        if (raw.Length <= 60)
        {
            return raw;
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))[..8].ToLowerInvariant();
        var tail = $"_{suffix}_{hash}";
        var prefixLength = Math.Max(1, 60 - tail.Length);
        return $"{normalized[..Math.Min(normalized.Length, prefixLength)]}{tail}";
    }
}
