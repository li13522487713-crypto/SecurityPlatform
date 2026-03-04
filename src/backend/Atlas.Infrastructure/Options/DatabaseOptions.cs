namespace Atlas.Infrastructure.Options;

public sealed class DatabaseOptions
{
    public string ConnectionString { get; init; } = "Data Source=atlas.db";
    public string DbType { get; init; } = "Sqlite";
}