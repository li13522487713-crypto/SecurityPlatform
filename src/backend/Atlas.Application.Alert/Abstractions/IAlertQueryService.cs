using Atlas.Application.Alert.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.Alert.Abstractions;

public interface IAlertQueryService
{
    Task<PagedResult<AlertListItem>> QueryAlertsAsync(
        PagedRequest request,
        TenantId tenantId,
        CancellationToken cancellationToken);
}