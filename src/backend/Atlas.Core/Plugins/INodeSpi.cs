namespace Atlas.Core.Plugins;

public interface INodeSpi
{
    string TypeKey { get; }
    string DisplayName { get; }
    Task<Dictionary<string, object>> ExecuteAsync(Dictionary<string, object> input, CancellationToken ct);
}
