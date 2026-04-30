using System.Text.Json.Serialization;

namespace Atlas.Application.Microflows.Models;

public sealed record MendixDomainModelDocumentDto
{
    [JsonPropertyName("appId")]
    public string AppId { get; init; } = string.Empty;

    [JsonPropertyName("workspaceId")]
    public string WorkspaceId { get; init; } = string.Empty;

    [JsonPropertyName("moduleId")]
    public string ModuleId { get; init; } = string.Empty;

    [JsonPropertyName("bindings")]
    public IReadOnlyList<MendixDomainModelBindingDto> Bindings { get; init; } = Array.Empty<MendixDomainModelBindingDto>();

    [JsonPropertyName("entities")]
    public IReadOnlyList<MendixDomainModelEntityDto> Entities { get; init; } = Array.Empty<MendixDomainModelEntityDto>();

    [JsonPropertyName("associations")]
    public IReadOnlyList<MendixDomainModelAssociationDto> Associations { get; init; } = Array.Empty<MendixDomainModelAssociationDto>();

    [JsonPropertyName("layout")]
    public MendixDomainModelLayoutDto Layout { get; init; } = new();

    [JsonPropertyName("syncState")]
    public MendixDomainModelSyncStateDto SyncState { get; init; } = new();
}

public sealed record MendixDomainModelBindingDto
{
    [JsonPropertyName("bindingId")]
    public string BindingId { get; init; } = string.Empty;

    [JsonPropertyName("sourceId")]
    public string SourceId { get; init; } = string.Empty;

    [JsonPropertyName("aiDatabaseId")]
    public string? AiDatabaseId { get; init; }

    [JsonPropertyName("alias")]
    public string Alias { get; init; } = string.Empty;

    [JsonPropertyName("driverCode")]
    public string DriverCode { get; init; } = string.Empty;

    [JsonPropertyName("defaultSchemaName")]
    public string? DefaultSchemaName { get; init; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; } = true;
}

public sealed record MendixDomainModelEntityDto
{
    [JsonPropertyName("entityId")]
    public string EntityId { get; init; } = string.Empty;

    [JsonPropertyName("bindingId")]
    public string BindingId { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("qualifiedName")]
    public string QualifiedName { get; init; } = string.Empty;

    [JsonPropertyName("schemaName")]
    public string SchemaName { get; init; } = string.Empty;

    [JsonPropertyName("tableName")]
    public string TableName { get; init; } = string.Empty;

    [JsonPropertyName("origin")]
    public string Origin { get; init; } = "imported";

    [JsonPropertyName("syncStatus")]
    public string SyncStatus { get; init; } = "clean";

    [JsonPropertyName("persistable")]
    public bool Persistable { get; init; } = true;

    [JsonPropertyName("attributes")]
    public IReadOnlyList<MendixDomainModelAttributeDto> Attributes { get; init; } = Array.Empty<MendixDomainModelAttributeDto>();
}

public sealed record MendixDomainModelAttributeDto
{
    [JsonPropertyName("attributeId")]
    public string AttributeId { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("columnName")]
    public string ColumnName { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = "string";

    [JsonPropertyName("required")]
    public bool Required { get; init; }

    [JsonPropertyName("primaryKey")]
    public bool PrimaryKey { get; init; }

    [JsonPropertyName("indexed")]
    public bool Indexed { get; init; }

    [JsonPropertyName("defaultValue")]
    public string? DefaultValue { get; init; }
}

public sealed record MendixDomainModelAssociationDto
{
    [JsonPropertyName("associationId")]
    public string AssociationId { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("fromEntityId")]
    public string FromEntityId { get; init; } = string.Empty;

    [JsonPropertyName("toEntityId")]
    public string ToEntityId { get; init; } = string.Empty;

    [JsonPropertyName("sourceAttributeId")]
    public string? SourceAttributeId { get; init; }

    [JsonPropertyName("targetAttributeId")]
    public string? TargetAttributeId { get; init; }

    [JsonPropertyName("owner")]
    public string Owner { get; init; } = "default";

    [JsonPropertyName("cardinality")]
    public string Cardinality { get; init; } = "oneToMany";

    [JsonPropertyName("bindingMode")]
    public string BindingMode { get; init; } = "logicalCrossDb";

    [JsonPropertyName("joinSpec")]
    public MendixDomainModelJoinSpecDto? JoinSpec { get; init; }
}

public sealed record MendixDomainModelJoinSpecDto
{
    [JsonPropertyName("sourceBindingId")]
    public string SourceBindingId { get; init; } = string.Empty;

    [JsonPropertyName("targetBindingId")]
    public string TargetBindingId { get; init; } = string.Empty;

    [JsonPropertyName("sourceField")]
    public string SourceField { get; init; } = string.Empty;

    [JsonPropertyName("targetField")]
    public string TargetField { get; init; } = string.Empty;

    [JsonPropertyName("joinType")]
    public string JoinType { get; init; } = "inner";
}

public sealed record MendixDomainModelLayoutDto
{
    [JsonPropertyName("entityFrames")]
    public IReadOnlyDictionary<string, MendixDomainModelEntityFrameDto> EntityFrames { get; init; } = new Dictionary<string, MendixDomainModelEntityFrameDto>(StringComparer.OrdinalIgnoreCase);
}

public sealed record MendixDomainModelEntityFrameDto
{
    [JsonPropertyName("x")]
    public double X { get; init; }

    [JsonPropertyName("y")]
    public double Y { get; init; }

    [JsonPropertyName("width")]
    public double Width { get; init; } = 280;

    [JsonPropertyName("height")]
    public double Height { get; init; } = 180;
}

public sealed record MendixDomainModelSyncStateDto
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = "idle";

    [JsonPropertyName("lastSyncedAt")]
    public DateTimeOffset? LastSyncedAt { get; init; }

    [JsonPropertyName("lastError")]
    public string? LastError { get; init; }
}

public sealed record MendixDomainModelImportTablesRequestDto
{
    [JsonPropertyName("sourceId")]
    public string SourceId { get; init; } = string.Empty;

    [JsonPropertyName("bindingId")]
    public string BindingId { get; init; } = string.Empty;

    [JsonPropertyName("schemaName")]
    public string SchemaName { get; init; } = string.Empty;

    [JsonPropertyName("tableNames")]
    public IReadOnlyList<string> TableNames { get; init; } = Array.Empty<string>();
}

public sealed record MendixDomainModelImportResultDto
{
    [JsonPropertyName("document")]
    public MendixDomainModelDocumentDto Document { get; init; } = new();

    [JsonPropertyName("importedEntityIds")]
    public IReadOnlyList<string> ImportedEntityIds { get; init; } = Array.Empty<string>();
}

public sealed record MendixDomainModelSyncPlanDto
{
    [JsonPropertyName("createTables")]
    public IReadOnlyList<MendixDomainModelCreateTablePlanDto> CreateTables { get; init; } = Array.Empty<MendixDomainModelCreateTablePlanDto>();

    [JsonPropertyName("addColumns")]
    public IReadOnlyList<MendixDomainModelAddColumnPlanDto> AddColumns { get; init; } = Array.Empty<MendixDomainModelAddColumnPlanDto>();

    [JsonPropertyName("warnings")]
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    [JsonPropertyName("errors")]
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}

public sealed record MendixDomainModelCreateTablePlanDto
{
    [JsonPropertyName("bindingId")]
    public string BindingId { get; init; } = string.Empty;

    [JsonPropertyName("schemaName")]
    public string SchemaName { get; init; } = string.Empty;

    [JsonPropertyName("tableName")]
    public string TableName { get; init; } = string.Empty;

    [JsonPropertyName("entityId")]
    public string EntityId { get; init; } = string.Empty;
}

public sealed record MendixDomainModelAddColumnPlanDto
{
    [JsonPropertyName("bindingId")]
    public string BindingId { get; init; } = string.Empty;

    [JsonPropertyName("schemaName")]
    public string SchemaName { get; init; } = string.Empty;

    [JsonPropertyName("tableName")]
    public string TableName { get; init; } = string.Empty;

    [JsonPropertyName("columnName")]
    public string ColumnName { get; init; } = string.Empty;
}

public sealed record MendixDomainModelSyncResultDto
{
    [JsonPropertyName("document")]
    public MendixDomainModelDocumentDto Document { get; init; } = new();

    [JsonPropertyName("plan")]
    public MendixDomainModelSyncPlanDto Plan { get; init; } = new();

    [JsonPropertyName("applied")]
    public bool Applied { get; init; }
}

public sealed record MendixDomainModelModuleSummaryDto
{
    [JsonPropertyName("moduleId")]
    public string ModuleId { get; init; } = string.Empty;

    [JsonPropertyName("entities")]
    public IReadOnlyList<MendixDomainModelEntityDto> Entities { get; init; } = Array.Empty<MendixDomainModelEntityDto>();
}

public sealed record MendixDomainModelMetadataCatalogDto
{
    public IReadOnlyList<MetadataEntityDto> Entities { get; init; } = Array.Empty<MetadataEntityDto>();

    public IReadOnlyList<MetadataAssociationDto> Associations { get; init; } = Array.Empty<MetadataAssociationDto>();
}
