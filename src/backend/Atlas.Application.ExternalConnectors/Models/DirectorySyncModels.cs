using Atlas.Domain.ExternalConnectors.Enums;

namespace Atlas.Application.ExternalConnectors.Models;

public sealed class ExternalDirectorySyncJobResponse
{
    public long Id { get; set; }

    public long ProviderId { get; set; }

    public DirectorySyncJobMode Mode { get; set; }

    public DirectorySyncJobStatus Status { get; set; }

    public string TriggerSource { get; set; } = string.Empty;

    public int DepartmentCreated { get; set; }

    public int DepartmentUpdated { get; set; }

    public int DepartmentDeleted { get; set; }

    public int UserCreated { get; set; }

    public int UserUpdated { get; set; }

    public int UserDeleted { get; set; }

    public int RelationChanged { get; set; }

    public int FailedItems { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }
}

public sealed class ExternalDirectorySyncDiffResponse
{
    public long Id { get; set; }

    public long JobId { get; set; }

    public DirectorySyncDiffType DiffType { get; set; }

    public string EntityId { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}
