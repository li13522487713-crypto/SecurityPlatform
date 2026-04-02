namespace Atlas.Infrastructure.Options;

public sealed class SqliteDisasterRecoveryOptions
{
    public bool Enabled { get; init; } = true;

    public int MaxAutoRetryCount { get; init; } = 1;

    public string QuarantineDirectory { get; init; } = "corrupted-databases";

    public bool KeepCorruptedFiles { get; init; } = true;
}
