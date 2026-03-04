namespace Atlas.Infrastructure.Options;

public sealed class PluginCatalogOptions
{
    /// <summary>
    /// 插件目录（相对 ContentRootPath）。
    /// </summary>
    public string RootPath { get; set; } = "plugins";
}
