using Atlas.Application.SetupConsole.Models;

namespace Atlas.Application.SetupConsole.Abstractions;

public interface IMigrationBulkWriter
{
    Task<long> PrepareTargetAsync(
        DataMigrationPlanItem item,
        ResolvedMigrationConnection source,
        ResolvedMigrationConnection target,
        string writeMode,
        bool createSchema,
        CancellationToken cancellationToken = default);

    Task<MigrationBatchWriteResult> WriteNextBatchAsync(
        DataMigrationPlanItem item,
        ResolvedMigrationConnection source,
        ResolvedMigrationConnection target,
        string writeMode,
        int batchSize,
        long lastMaxId,
        CancellationToken cancellationToken = default);

    Task<long> CountTargetRowsAsync(
        DataMigrationPlanItem item,
        ResolvedMigrationConnection target,
        CancellationToken cancellationToken = default);
}

public sealed record MigrationBatchWriteResult(
    bool HasRows,
    int BatchNoRows,
    long LastMaxId,
    bool UsedBulkCopy,
    string? WarningMessage = null);
