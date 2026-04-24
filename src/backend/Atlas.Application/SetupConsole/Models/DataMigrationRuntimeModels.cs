namespace Atlas.Application.SetupConsole.Models;

public sealed record ResolvedMigrationConnection(
    string DriverCode,
    string DbType,
    string Mode,
    string ConnectionString,
    string DisplayName,
    long? DataSourceId,
    long? AiDatabaseId,
    IReadOnlyList<ResolvedMigrationTable> Tables,
    string Fingerprint);

public sealed record ResolvedMigrationTable(
    string EntityName,
    string TableName,
    string KeyColumn,
    bool SupportsResume,
    Type? EntityType = null,
    string Kind = "table");

public sealed record DataMigrationPlan(
    IReadOnlyList<DataMigrationPlanItem> Items,
    long TotalRows,
    int TotalEntities,
    int EstimatedBatches,
    IReadOnlyList<string> UnsupportedTables,
    IReadOnlyList<string> TargetNonEmptyTables,
    IReadOnlyList<string> MissingTargetTables,
    IReadOnlyList<string> Warnings);

public sealed record DataMigrationPlanItem(
    string EntityName,
    string TableName,
    string KeyColumn,
    bool SupportsResume,
    Type? EntityType,
    string Kind,
    long SourceRows,
    long TargetRowsBefore,
    int TotalBatchCount);
