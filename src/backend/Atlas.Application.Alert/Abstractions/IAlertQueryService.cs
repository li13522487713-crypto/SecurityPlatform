using Atlas.Application.Alert.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.Alert.Abstractions;

public interface IAlertQueryService
{
    PagedResult<AlertListItem> QueryAlerts(PagedRequest request, TenantId tenantId);
}