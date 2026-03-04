using Atlas.Application.Plugins.Abstractions;
using Atlas.Application.Plugins.Models;
using Atlas.Core.Plugins;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Plugins;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Services;

public sealed class PluginCatalogService : IPluginCatalogService
{
    private readonly PluginCatalogOptions _options;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<PluginCatalogService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<LoadedPluginContext> _loadedContexts = new();
    private readonly List<PluginDescriptor> _descriptors = new();
    private bool _initialized;

    public PluginCatalogService(
        IOptions<PluginCatalogOptions> options,
        IHostEnvironment hostEnvironment,
        ILogger<PluginCatalogService> logger)
    {
        _options = options.Value;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PluginDescriptor>> GetPluginsAsync(CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return _descriptors.ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ReloadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await UnloadAllInternalAsync(cancellationToken);
            await LoadAllInternalAsync(cancellationToken);
            _initialized = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            await LoadAllInternalAsync(cancellationToken);
            _initialized = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task LoadAllInternalAsync(CancellationToken cancellationToken)
    {
        _descriptors.Clear();
        var pluginRoot = ResolvePluginRoot();
        Directory.CreateDirectory(pluginRoot);
        var assemblyFiles = Directory.GetFiles(pluginRoot, "*.dll", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (var file in assemblyFiles)
        {
            var loadedAt = DateTimeOffset.UtcNow;
            try
            {
                var loadContext = new PluginLoadContext(file);
                var assembly = loadContext.LoadFromAssemblyPath(file);
                var pluginType = assembly
                    .GetTypes()
                    .FirstOrDefault(type => !type.IsAbstract && typeof(IAtlasPlugin).IsAssignableFrom(type));
                if (pluginType is null)
                {
                    _descriptors.Add(new PluginDescriptor(
                        Path.GetFileNameWithoutExtension(file),
                        Path.GetFileNameWithoutExtension(file),
                        "Unknown",
                        assembly.GetName().Name ?? Path.GetFileNameWithoutExtension(file),
                        file,
                        "NoEntryPoint",
                        loadedAt,
                        "未找到 IAtlasPlugin 实现"));
                    loadContext.Unload();
                    continue;
                }

                var plugin = Activator.CreateInstance(pluginType) as IAtlasPlugin;
                if (plugin is null)
                {
                    _descriptors.Add(new PluginDescriptor(
                        pluginType.Name,
                        pluginType.Name,
                        "Unknown",
                        assembly.GetName().Name ?? pluginType.Assembly.GetName().Name ?? pluginType.Name,
                        file,
                        "Failed",
                        loadedAt,
                        "插件实例化失败"));
                    loadContext.Unload();
                    continue;
                }

                await plugin.OnLoadedAsync(cancellationToken);
                _loadedContexts.Add(new LoadedPluginContext(loadContext, plugin));
                _descriptors.Add(new PluginDescriptor(
                    plugin.Code,
                    plugin.Name,
                    plugin.Version,
                    assembly.GetName().Name ?? pluginType.Name,
                    file,
                    "Loaded",
                    loadedAt,
                    null));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Load plugin failed: {PluginFile}", file);
                _descriptors.Add(new PluginDescriptor(
                    Path.GetFileNameWithoutExtension(file),
                    Path.GetFileNameWithoutExtension(file),
                    "Unknown",
                    Path.GetFileNameWithoutExtension(file),
                    file,
                    "Failed",
                    loadedAt,
                    ex.Message));
            }
        }
    }

    private async Task UnloadAllInternalAsync(CancellationToken cancellationToken)
    {
        foreach (var loaded in _loadedContexts)
        {
            try
            {
                await loaded.Plugin.OnUnloadingAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Plugin unloading callback failed: {PluginCode}", loaded.Plugin.Code);
            }

            loaded.LoadContext.Unload();
        }

        _loadedContexts.Clear();
        _descriptors.Clear();
    }

    private string ResolvePluginRoot()
    {
        if (Path.IsPathRooted(_options.RootPath))
        {
            return _options.RootPath;
        }

        return Path.Combine(_hostEnvironment.ContentRootPath, _options.RootPath);
    }

    private sealed record LoadedPluginContext(PluginLoadContext LoadContext, IAtlasPlugin Plugin);
}
