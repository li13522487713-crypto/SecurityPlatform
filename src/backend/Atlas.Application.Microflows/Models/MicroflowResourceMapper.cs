using System.Text.Json;
using Atlas.Domain.Microflows.Entities;

namespace Atlas.Application.Microflows.Models;

public static class MicroflowResourceMapper
{
    public static MicroflowResourceDto ToDto(MicroflowResourceEntity entity, MicroflowSchemaSnapshotEntity? snapshot = null)
    {
        return new MicroflowResourceDto
        {
            Id = entity.Id,
            SchemaId = entity.SchemaId ?? snapshot?.Id ?? string.Empty,
            WorkspaceId = entity.WorkspaceId,
            ModuleId = entity.ModuleId,
            ModuleName = entity.ModuleName,
            FolderId = entity.FolderId,
            FolderPath = entity.FolderPath,
            Name = entity.Name,
            DisplayName = entity.DisplayName,
            Description = entity.Description,
            Tags = DeserializeTags(entity.TagsJson),
            OwnerId = entity.OwnerId,
            OwnerName = entity.OwnerName,
            Version = entity.Version,
            LatestPublishedVersion = entity.LatestPublishedVersion,
            Status = entity.Status,
            PublishStatus = entity.PublishStatus,
            Favorite = entity.Favorite,
            Archived = entity.Archived,
            ReferenceCount = entity.ReferenceCount,
            LastRunStatus = entity.LastRunStatus,
            LastRunAt = entity.LastRunAt,
            CreatedBy = entity.CreatedBy,
            CreatedAt = entity.CreatedAt,
            UpdatedBy = entity.UpdatedBy,
            UpdatedAt = entity.UpdatedAt,
            Schema = ParseSchema(snapshot?.SchemaJson),
            Permissions = BuildPermissions(entity)
        };
    }

    public static JsonElement? ParseSchemaJson(string? schemaJson) => ParseSchema(schemaJson);

    public static IReadOnlyList<string> ReadTags(string? tagsJson) => DeserializeTags(tagsJson);

    private static MicroflowResourcePermissionsDto BuildPermissions(MicroflowResourceEntity entity)
    {
        var editable = !entity.Archived && !string.Equals(entity.Status, "archived", StringComparison.OrdinalIgnoreCase);
        return new MicroflowResourcePermissionsDto
        {
            CanEdit = editable,
            CanDelete = true,
            CanPublish = editable,
            CanArchive = editable,
            CanDuplicate = true
        };
    }

    private static IReadOnlyList<string> DeserializeTags(string? tagsJson)
    {
        if (string.IsNullOrWhiteSpace(tagsJson))
        {
            return Array.Empty<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(tagsJson) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    private static JsonElement? ParseSchema(string? schemaJson)
    {
        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(schemaJson);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
