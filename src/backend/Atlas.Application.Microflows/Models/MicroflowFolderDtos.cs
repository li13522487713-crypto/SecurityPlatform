using System.Text.Json.Serialization;

namespace Atlas.Application.Microflows.Models;

public record MicroflowFolderDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("workspaceId")]
    public string? WorkspaceId { get; init; }

    [JsonPropertyName("moduleId")]
    public string ModuleId { get; init; } = string.Empty;

    [JsonPropertyName("parentFolderId")]
    public string? ParentFolderId { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    [JsonPropertyName("depth")]
    public int Depth { get; init; }

    [JsonPropertyName("createdBy")]
    public string? CreatedBy { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("updatedBy")]
    public string? UpdatedBy { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed record MicroflowFolderTreeNodeDto : MicroflowFolderDto
{
    [JsonPropertyName("children")]
    public IReadOnlyList<MicroflowFolderTreeNodeDto> Children { get; init; } = Array.Empty<MicroflowFolderTreeNodeDto>();
}

public sealed record CreateMicroflowFolderRequestDto
{
    [JsonPropertyName("workspaceId")]
    public string? WorkspaceId { get; init; }

    [JsonPropertyName("moduleId")]
    public string ModuleId { get; init; } = string.Empty;

    [JsonPropertyName("parentFolderId")]
    public string? ParentFolderId { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}

public sealed record RenameMicroflowFolderRequestDto
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}

public sealed record MoveMicroflowFolderRequestDto
{
    [JsonPropertyName("parentFolderId")]
    public string? ParentFolderId { get; init; }
}

public sealed record MoveMicroflowRequestDto
{
    [JsonPropertyName("targetFolderId")]
    public string? TargetFolderId { get; init; }
}
