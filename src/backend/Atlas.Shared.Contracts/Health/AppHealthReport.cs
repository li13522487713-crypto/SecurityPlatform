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
}
