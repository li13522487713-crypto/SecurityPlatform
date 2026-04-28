using Atlas.Domain.Microflows.Entities;

namespace Atlas.Application.Microflows.Models;

public static class MicroflowFolderMapper
{
    public static MicroflowFolderDto ToDto(MicroflowFolderEntity entity)
    {
        return new MicroflowFolderDto
        {
            Id = entity.Id,
            WorkspaceId = entity.WorkspaceId,
            ModuleId = entity.ModuleId,
            ParentFolderId = entity.ParentFolderId,
            Name = entity.Name,
            Path = entity.Path,
            Depth = entity.Depth,
            CreatedBy = entity.CreatedBy,
            CreatedAt = entity.CreatedAt,
            UpdatedBy = entity.UpdatedBy,
            UpdatedAt = entity.UpdatedAt
        };
    }

    public static MicroflowFolderTreeNodeDto ToTreeNode(
        MicroflowFolderEntity entity,
        IReadOnlyList<MicroflowFolderTreeNodeDto> children)
    {
        return new MicroflowFolderTreeNodeDto
        {
            Id = entity.Id,
            WorkspaceId = entity.WorkspaceId,
            ModuleId = entity.ModuleId,
            ParentFolderId = entity.ParentFolderId,
            Name = entity.Name,
            Path = entity.Path,
            Depth = entity.Depth,
            CreatedBy = entity.CreatedBy,
            CreatedAt = entity.CreatedAt,
            UpdatedBy = entity.UpdatedBy,
            UpdatedAt = entity.UpdatedAt,
            Children = children
        };
    }
}
