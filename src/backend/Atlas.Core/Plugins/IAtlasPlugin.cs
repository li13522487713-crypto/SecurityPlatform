namespace Atlas.Core.Plugins;

/// <summary>
/// Atlas 插件入口约定。
/// </summary>
public interface IAtlasPlugin
{
    string Code { get; }
    string Name { get; }
    string Version { get; }

    Task OnLoadedAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    Task OnUnloadingAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
