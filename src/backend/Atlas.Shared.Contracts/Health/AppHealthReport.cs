using Atlas.Shared.Contracts.Process;

namespace Atlas.Shared.Contracts.Health;

public sealed record AppHealthReport
{
    public AppProcessStatus Status { get; init; } = AppProcessStatus.Unknown;

    public bool Live { get; init; }

    public bool Ready { get; init; }

    public string Version { get; init; } = string.Empty;

    public string? Message { get; init; }

    public DateTimeOffset CheckedAt { get; init; } = DateTimeOffset.UtcNow;

    public string? AppKey { get; init; }

    public string? InstanceId { get; init; }

    public string? TenantId { get; init; }

    public string? ReleaseVersion { get; init; }

    public double UptimeSeconds { get; init; }

    public bool DbConnected { get; init; }

    public string? MigrationStatus { get; init; }
}
