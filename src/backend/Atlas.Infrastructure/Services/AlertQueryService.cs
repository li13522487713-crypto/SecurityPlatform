using Atlas.Application.Alert.Abstractions;
using Atlas.Application.Alert.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services;

public sealed class AlertQueryService : IAlertQueryService
{
    public PagedResult<AlertListItem> QueryAlerts(PagedRequest request, TenantId tenantId)
    {
        var items = Array.Empty<AlertListItem>();
        return new PagedResult<AlertListItem>(items, 0, request.PageIndex, request.PageSize);
    }
}