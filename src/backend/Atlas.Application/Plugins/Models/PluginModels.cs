using Atlas.Core.Plugins;

namespace Atlas.Application.Plugins.Models;

/// <summary>插件生命周期状态枚举</summary>
public enum PluginLifecycleState
{
    /// <summary>插件已安装（但尚未启用）</summary>
    Installed = 0,
    /// <summary>插件已加载并启用</summary>
    Enabled = 1,
    /// <summary>插件已禁用（保留安装，可重新启用）</summary>
    Disabled = 2,
    /// <summary>插件已卸载（移除）</summary>
    Uninstalled = 3,
    /// <summary>加载失败</summary>
    LoadFailed = 4
}

public sealed record PluginDescriptor(
    string Code,
    string Name,
    string Version,
    string Description,
    string Author,
    string? IconUrl,
    PluginCategory Category,
    IReadOnlyList<PluginDependency> Dependencies,
    IReadOnlyList<string> RequiredPermissions,
    string? ConfigSchema,
    string AssemblyName,
    string FilePath,
    string State,
    DateTimeOffset LoadedAt,
    string? ErrorMessage)
{
    public PluginLifecycleState LifecycleState => State switch
    {
        "Disabled" => PluginLifecycleState.Disabled,
        "Loaded" or "Enabled" => PluginLifecycleState.Enabled,
        "Failed" => PluginLifecycleState.LoadFailed,
        "Uninstalled" => PluginLifecycleState.Uninstalled,
        _ => PluginLifecycleState.Installed
    };
}
