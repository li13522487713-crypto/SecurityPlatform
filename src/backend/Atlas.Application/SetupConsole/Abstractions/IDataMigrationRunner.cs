namespace Atlas.Application.SetupConsole.Abstractions;

public interface IDataMigrationRunner
{
    Task RunAsync(long jobId, CancellationToken cancellationToken = default);
}
