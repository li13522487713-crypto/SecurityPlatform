namespace Atlas.Infrastructure.Options;

public sealed class AiDatabaseHostingOptions
{
    public string SqliteRoot { get; set; } = "data/ai-db";

    public string? MySqlAdminConnection { get; set; }

    public string? PostgreSqlAdminConnection { get; set; }
}
