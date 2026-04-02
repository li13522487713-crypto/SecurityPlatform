namespace Atlas.Core.Plugins;

public interface IPluginRegistry
{
    void RegisterNode(INodeSpi spi);
    void RegisterFunction(IFunctionSpi spi);
    void RegisterDataSource(IDataSourceSpi spi);
    void RegisterTemplate(ITemplateSpi spi);
    INodeSpi? GetNode(string typeKey);
    IFunctionSpi? GetFunction(string name);
    IDataSourceSpi? GetDataSource(string providerKey);
    ITemplateSpi? GetTemplate(string templateKey);
    IReadOnlyList<PluginInfo> GetAll();
}

public sealed record PluginInfo(string PluginType, string Key, string DisplayName);
