namespace Atlas.Domain.DynamicTables.Enums;

public enum SchemaDraftObjectType
{
    Table = 0,
    Field = 1,
    Index = 2,
    Relation = 3
}

public enum SchemaDraftChangeType
{
    Create = 0,
    Update = 1,
    Delete = 2
}

public enum SchemaDraftRiskLevel
{
    Low = 0,
    Medium = 1,
    High = 2
}

public enum SchemaDraftStatus
{
    Pending = 0,
    Validated = 1,
    Published = 2,
    Abandoned = 3
}
