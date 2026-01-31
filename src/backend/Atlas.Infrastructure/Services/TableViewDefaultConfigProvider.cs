using Atlas.Application.Options;
using Atlas.Application.TableViews.Abstractions;
using Atlas.Application.TableViews.Models;
using Atlas.Core.Tenancy;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Services;

public sealed class TableViewDefaultConfigProvider : ITableViewDefaultConfigProvider
{
    private readonly IOptionsMonitor<TableViewDefaultOptions> _options;

    public TableViewDefaultConfigProvider(IOptionsMonitor<TableViewDefaultOptions> options)
    {
        _options = options;
    }

    public Task<TableViewConfig> GetDefaultConfigAsync(
        TenantId tenantId,
        string tableKey,
        CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        if (!string.IsNullOrWhiteSpace(tableKey) && options.Defaults.Count > 0)
        {
            if (options.Defaults.TryGetValue(tableKey, out var config))
            {
                return Task.FromResult(config);
            }
        }

        if (options.Fallback is not null)
        {
            return Task.FromResult(options.Fallback);
        }

        return Task.FromResult(BuildDefaultConfig());
    }

    private static TableViewConfig BuildDefaultConfig()
    {
        return new TableViewConfig
        {
            Density = "default",
            Pagination = new TableViewPagination { PageSize = 10 }
        };
    }
}
