namespace Atlas.Domain.DynamicTables.Enums;

public enum SchemaChangeTaskStatus
{
    Pending = 0,
    Validating = 1,
    WaitingApproval = 2,
    Applying = 3,
    Applied = 4,
    Failed = 5,
    RolledBack = 6,
    Cancelled = 7
}
