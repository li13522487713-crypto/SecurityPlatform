namespace Atlas.Domain.ExternalConnectors.Enums;

public enum DirectorySyncJobMode
{
    Full = 1,
    Incremental = 2,
}

public enum DirectorySyncJobStatus
{
    Pending = 1,
    Running = 2,
    Succeeded = 3,
    Failed = 4,
    PartialSucceeded = 5,
    Canceled = 6,
}

public enum DirectorySyncDiffType
{
    DepartmentCreated = 1,
    DepartmentUpdated = 2,
    DepartmentDeleted = 3,
    UserCreated = 4,
    UserUpdated = 5,
    UserDeleted = 6,
    RelationCreated = 7,
    RelationDeleted = 8,
}
