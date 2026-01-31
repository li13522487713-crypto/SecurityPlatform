using Atlas.Application.TableViews.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.TableViews.Abstractions;

public interface ITableViewDefaultConfigProvider
{
    Task<TableViewConfig> GetDefaultConfigAsync(
        TenantId tenantId,
        string tableKey,
        CancellationToken cancellationToken);
}
