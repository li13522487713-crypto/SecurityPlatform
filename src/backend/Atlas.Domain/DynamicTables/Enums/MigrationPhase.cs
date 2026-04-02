namespace Atlas.Domain.DynamicTables.Enums;

/// <summary>
/// 三阶段迁移状态机：Expand → Migrate → Contract
/// </summary>
public enum MigrationPhase
{
    Pending = 0,
    Expanding = 1,
    Expanded = 2,
    Migrating = 3,
    Migrated = 4,
    Contracting = 5,
    Contracted = 6,
    Completed = 7,
    Failed = 8,
    RolledBack = 9
}
