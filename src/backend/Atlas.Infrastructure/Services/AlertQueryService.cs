using Atlas.Application.Alert.Abstractions;
using Atlas.Application.Alert.Models;
using Atlas.Application.Identity.Abstractions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services;

public sealed class AlertQueryService : IAlertQueryService
{
    private readonly ITenantDataScopeFilter _dataScopeFilter;

    public AlertQueryService(ITenantDataScopeFilter dataScopeFilter)
    {
        _dataScopeFilter = dataScopeFilter;
    }

    public async Task<PagedResult<AlertListItem>> QueryAlertsAsync(
        PagedRequest request,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;
        _ = await _dataScopeFilter.GetEffectiveScopeAsync(cancellationToken);
        var total = 0;
        var items = Array.Empty<AlertListItem>();
        var result = new PagedResult<AlertListItem>(items, total, pageIndex, pageSize);
        return result;
    }
}