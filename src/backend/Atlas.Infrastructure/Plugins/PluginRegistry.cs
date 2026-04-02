using Atlas.Core.Plugins;

namespace Atlas.Infrastructure.Plugins;

public sealed class PluginRegistry : IPluginRegistry
{
    private readonly Dictionary<string, INodeSpi> _nodes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IFunctionSpi> _functions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IDataSourceSpi> _dataSources = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ITemplateSpi> _templates = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public void RegisterNode(INodeSpi spi)
    {
        lock (_gate)
            _nodes[spi.TypeKey] = spi;
    }

    public void RegisterFunction(IFunctionSpi spi)
    {
        lock (_gate)
            _functions[spi.Name] = spi;
    }

    public void RegisterDataSource(IDataSourceSpi spi)
    {
        lock (_gate)
            _dataSources[spi.ProviderKey] = spi;
    }

    public void RegisterTemplate(ITemplateSpi spi)
    {
        lock (_gate)
            _templates[spi.TemplateKey] = spi;
    }

    public INodeSpi? GetNode(string typeKey)
    {
        lock (_gate)
            return _nodes.GetValueOrDefault(typeKey);
    }

    public IFunctionSpi? GetFunction(string name)
    {
        lock (_gate)
            return _functions.GetValueOrDefault(name);
    }

    public IDataSourceSpi? GetDataSource(string providerKey)
    {
        lock (_gate)
            return _dataSources.GetValueOrDefault(providerKey);
    }

    public ITemplateSpi? GetTemplate(string templateKey)
    {
        lock (_gate)
            return _templates.GetValueOrDefault(templateKey);
    }

    public IReadOnlyList<PluginInfo> GetAll()
    {
        lock (_gate)
        {
            var list = new List<PluginInfo>();
            foreach (var kv in _nodes)
                list.Add(new PluginInfo("Node", kv.Key, kv.Value.DisplayName));
            foreach (var kv in _functions)
                list.Add(new PluginInfo("Function", kv.Key, kv.Key));
            foreach (var kv in _dataSources)
                list.Add(new PluginInfo("DataSource", kv.Key, kv.Key));
            foreach (var kv in _templates)
                list.Add(new PluginInfo("Template", kv.Key, kv.Key));
            return list;
        }
    }
}
