using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Abstractions;

public interface IMicroflowAppAssetService
{
    Task<MicroflowAppAssetDto> GetAppAsync(string appId, string workspaceId, CancellationToken cancellationToken);

    Task<IReadOnlyList<MicroflowModuleAssetDto>> ListModulesAsync(string appId, string workspaceId, CancellationToken cancellationToken);
}
