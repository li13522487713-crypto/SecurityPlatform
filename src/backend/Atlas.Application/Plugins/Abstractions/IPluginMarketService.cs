using Atlas.Core.Plugins;
using Atlas.Domain.Plugins;

namespace Atlas.Application.Plugins.Abstractions;

public interface IPluginMarketQueryService
{
    Task<(IReadOnlyList<PluginMarketEntry> Items, int Total)> SearchAsync(
        string? keyword, PluginCategory? category, int pageIndex, int pageSize, CancellationToken cancellationToken);

    Task<PluginMarketEntry?> GetByCodeAsync(string code, CancellationToken cancellationToken);

    Task<IReadOnlyList<PluginMarketVersion>> GetVersionsAsync(long entryId, CancellationToken cancellationToken);
}

public interface IPluginMarketCommandService
{
    Task<long> PublishAsync(PublishPluginMarketRequest request, Guid tenantId, CancellationToken cancellationToken);
    Task UpdateAsync(long id, UpdatePluginMarketRequest request, CancellationToken cancellationToken);
    Task DeprecateAsync(long id, CancellationToken cancellationToken);
    Task RateAsync(long entryId, Guid tenantId, int rating, CancellationToken cancellationToken);
}

public sealed record PublishPluginMarketRequest(
    string Code,
    string Name,
    string Description,
    string Author,
    PluginCategory Category,
    string Version,
    string? IconUrl,
    string? PackageUrl,
    string? ReleaseNotes);

public sealed record UpdatePluginMarketRequest(
    string Name,
    string Description,
    string? IconUrl);
