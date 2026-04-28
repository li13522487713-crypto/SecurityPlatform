using Atlas.Application.SetupConsole.Models;

namespace Atlas.Application.SetupConsole.Abstractions;

public interface IMigrationConnectionResolver
{
    Task<ResolvedMigrationConnection> ResolveAsync(
        DbConnectionConfig config,
        CancellationToken cancellationToken = default);

    DbConnectionConfig Mask(DbConnectionConfig config);
}
