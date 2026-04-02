namespace Atlas.Core.Plugins;

public interface IDataSourceSpi
{
    string ProviderKey { get; }
    Task<IReadOnlyList<Dictionary<string, object>>> QueryAsync(string query, Dictionary<string, object>? parameters, CancellationToken ct);
    Task<int> ExecuteAsync(string command, Dictionary<string, object>? parameters, CancellationToken ct);
}
