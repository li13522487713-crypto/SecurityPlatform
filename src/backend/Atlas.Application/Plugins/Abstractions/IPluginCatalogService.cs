using Atlas.Application.Plugins.Models;

namespace Atlas.Application.Plugins.Abstractions;

public interface IPluginCatalogService
{
    Task<IReadOnlyList<PluginDescriptor>> GetPluginsAsync(CancellationToken cancellationToken);

    Task ReloadAsync(CancellationToken cancellationToken);
}
