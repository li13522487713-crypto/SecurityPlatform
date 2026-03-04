using System.Runtime.Loader;

namespace Atlas.Infrastructure.Plugins;

internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath)
        : base($"AtlasPlugin:{Path.GetFileNameWithoutExtension(pluginPath)}", isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override System.Reflection.Assembly? Load(System.Reflection.AssemblyName assemblyName)
    {
        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        if (!string.IsNullOrWhiteSpace(path))
        {
            return LoadFromAssemblyPath(path);
        }

        return null;
    }
}
