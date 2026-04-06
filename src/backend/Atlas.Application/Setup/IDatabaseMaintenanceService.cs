namespace Atlas.Application.Setup;

public interface IDatabaseMaintenanceService
{
    Task<DatabaseConnectionStatus> TestConnectionAsync(CancellationToken cancellationToken = default);

    Task<BackupResult> BackupNowAsync(CancellationToken cancellationToken = default);

    Task RestoreFromBackupAsync(string backupFileName, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BackupFileInfo>> ListBackupsAsync(CancellationToken cancellationToken = default);

    Task<DatabaseInfo> GetDatabaseInfoAsync(CancellationToken cancellationToken = default);
}

public sealed record DatabaseConnectionStatus(bool Connected, string Message, long? LatencyMs);

public sealed record BackupResult(bool Success, string? FileName, string? Message, long? SizeBytes);

public sealed record BackupFileInfo(
    string FileName,
    long SizeBytes,
    DateTimeOffset CreatedAt,
    string? Sha256);

public sealed record DatabaseInfo(
    string DbType,
    string ConnectionString,
    long? FileSizeBytes,
    string? JournalMode,
    long? PageCount,
    long? PageSize);
