namespace Atlas.Infrastructure.Options;

public sealed class AiDatabaseHostingOptions
{
    public AiDatabaseSqliteHostingOptions Sqlite { get; set; } = new();

    public AiDatabaseMySqlHostingOptions MySql { get; set; } = new();

    public AiDatabasePostgreSqlHostingOptions PostgreSql { get; set; } = new();

    public string DefaultDriverCode { get; set; } = "SQLite";

    public int PreviewLimit { get; set; } = 100;

    public int CommandTimeoutSeconds { get; set; } = 10;

    [Obsolete("Use Sqlite.Root.")]
    public string SqliteRoot
    {
        get => Sqlite.Root;
        set => Sqlite.Root = value;
    }

    [Obsolete("Use MySql.AdminConnection.")]
    public string? MySqlAdminConnection
    {
        get => MySql.AdminConnection;
        set => MySql.AdminConnection = value;
    }

    [Obsolete("Use PostgreSql.AdminConnection.")]
    public string? PostgreSqlAdminConnection
    {
        get => PostgreSql.AdminConnection;
        set => PostgreSql.AdminConnection = value;
    }
}

public sealed class AiDatabaseSqliteHostingOptions
{
    public string Root { get; set; } = "data/ai-db";
}

public sealed class AiDatabaseMySqlHostingOptions
{
    public string? AdminConnection { get; set; }

    public string Charset { get; set; } = "utf8mb4";

    public string Collation { get; set; } = "utf8mb4_0900_ai_ci";
}

public sealed class AiDatabasePostgreSqlHostingOptions
{
    public string? AdminConnection { get; set; }

    public string ProvisionMode { get; set; } = "Schema";
}
