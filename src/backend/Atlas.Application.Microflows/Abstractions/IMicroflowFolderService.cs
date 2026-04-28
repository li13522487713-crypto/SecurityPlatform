using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Abstractions;

public interface IMicroflowFolderService
{
    Task<IReadOnlyList<MicroflowFolderDto>> ListAsync(string? workspaceId, string moduleId, CancellationToken cancellationToken);

    Task<IReadOnlyList<MicroflowFolderTreeNodeDto>> GetTreeAsync(string? workspaceId, string moduleId, CancellationToken cancellationToken);

    Task<MicroflowFolderDto> CreateAsync(CreateMicroflowFolderRequestDto request, CancellationToken cancellationToken);

    Task<MicroflowFolderDto> RenameAsync(string id, RenameMicroflowFolderRequestDto request, CancellationToken cancellationToken);

    Task<MicroflowFolderDto> MoveAsync(string id, MoveMicroflowFolderRequestDto request, CancellationToken cancellationToken);

    Task DeleteAsync(string id, CancellationToken cancellationToken);
}
