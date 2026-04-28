using System.Text.Json;
using System.Text.Json.Serialization;

namespace Atlas.Application.Microflows.Models;

public sealed record ListMicroflowsRequestDto
{
    [JsonPropertyName("pageIndex")]
    public int PageIndex { get; init; } = 1;

    [JsonPropertyName("pageSize")]
    public int PageSize { get; init; } = 20;

    [JsonPropertyName("sortBy")]
    public string? SortBy { get; init; }

    [JsonPropertyName("sortOrder")]
    public string? SortOrder { get; init; }

    [JsonPropertyName("workspaceId")]
    public string? WorkspaceId { get; init; }

    [JsonPropertyName("keyword")]
    public string? Keyword { get; init; }

    [JsonPropertyName("status")]
    public IReadOnlyList<string> Status { get; init; } = Array.Empty<string>();

    [JsonPropertyName("publishStatus")]
    public IReadOnlyList<string> PublishStatus { get; init; } = Array.Empty<string>();

    [JsonPropertyName("favoriteOnly")]
    public bool FavoriteOnly { get; init; }

    [JsonPropertyName("ownerId")]
    public string? OwnerId { get; init; }

    [JsonPropertyName("moduleId")]
    public string? ModuleId { get; init; }

    [JsonPropertyName("tags")]
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    [JsonPropertyName("updatedFrom")]
    public DateTimeOffset? UpdatedFrom { get; init; }

    [JsonPropertyName("updatedTo")]
    public DateTimeOffset? UpdatedTo { get; init; }
}

public sealed record CreateMicroflowRequestDto
{
    [JsonPropertyName("workspaceId")]
    public string? WorkspaceId { get; init; }

    [JsonPropertyName("input")]
    public MicroflowCreateInputDto Input { get; init; } = new();
}

public sealed record MicroflowCreateInputDto
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("moduleId")]
    public string ModuleId { get; init; } = string.Empty;

    [JsonPropertyName("moduleName")]
    public string? ModuleName { get; init; }

    [JsonPropertyName("tags")]
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    [JsonPropertyName("parameters")]
    public JsonElement? Parameters { get; init; }

    [JsonPropertyName("returnType")]
    public JsonElement? ReturnType { get; init; }

    [JsonPropertyName("returnVariableName")]
    public string? ReturnVariableName { get; init; }

    [JsonPropertyName("security")]
    public JsonElement? Security { get; init; }

    [JsonPropertyName("concurrency")]
    public JsonElement? Concurrency { get; init; }

    [JsonPropertyName("exposure")]
    public JsonElement? Exposure { get; init; }

    [JsonPropertyName("template")]
    public string? Template { get; init; }

    [JsonPropertyName("schema")]
    public JsonElement? Schema { get; init; }
}

public sealed record UpdateMicroflowResourceRequestDto
{
    [JsonPropertyName("patch")]
    public MicroflowResourcePatchDto Patch { get; init; } = new();
}

public sealed record MicroflowResourcePatchDto
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("tags")]
    public IReadOnlyList<string>? Tags { get; init; }

    [JsonPropertyName("moduleId")]
    public string? ModuleId { get; init; }

    [JsonPropertyName("moduleName")]
    public string? ModuleName { get; init; }

    [JsonPropertyName("ownerId")]
    public string? OwnerId { get; init; }

    [JsonPropertyName("ownerName")]
    public string? OwnerName { get; init; }
}

public sealed record SaveMicroflowSchemaRequestDto
{
    [JsonPropertyName("schema")]
    public JsonElement? Schema { get; init; }

    [JsonPropertyName("baseVersion")]
    public string? BaseVersion { get; init; }

    [JsonPropertyName("schemaId")]
    public string? SchemaId { get; init; }

    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("saveReason")]
    public string? SaveReason { get; init; }

    [JsonPropertyName("clientRequestId")]
    public string? ClientRequestId { get; init; }

    [JsonPropertyName("force")]
    public bool Force { get; init; }
}

public sealed record SaveMicroflowSchemaResponseDto
{
    [JsonPropertyName("resource")]
    public MicroflowResourceDto Resource { get; init; } = new();

    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; init; } = string.Empty;

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; init; }

    [JsonPropertyName("changedAfterPublish")]
    public bool ChangedAfterPublish { get; init; }
}

public sealed record GetMicroflowSchemaResponseDto
{
    [JsonPropertyName("resourceId")]
    public string ResourceId { get; init; } = string.Empty;

    [JsonPropertyName("schema")]
    public JsonElement Schema { get; init; }

    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; init; } = string.Empty;

    [JsonPropertyName("migrationVersion")]
    public string? MigrationVersion { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; init; }

    [JsonPropertyName("updatedBy")]
    public string? UpdatedBy { get; init; }
}

public sealed record DuplicateMicroflowRequestDto
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("moduleId")]
    public string? ModuleId { get; init; }

    [JsonPropertyName("moduleName")]
    public string? ModuleName { get; init; }

    [JsonPropertyName("tags")]
    public IReadOnlyList<string>? Tags { get; init; }
}

public sealed record RenameMicroflowRequestDto
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }
}

public sealed record ToggleFavoriteMicroflowRequestDto
{
    [JsonPropertyName("favorite")]
    public bool Favorite { get; init; }
}

public sealed record DeleteMicroflowResponseDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;
}
