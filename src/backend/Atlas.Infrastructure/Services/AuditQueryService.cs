using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Audit.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services;

public sealed class AuditQueryService : IAuditQueryService
{
    public Task<PagedResult<AuditListItem>> QueryAuditsAsync(
        PagedRequest request,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;
        var total = 0;
        var items = Array.Empty<AuditListItem>();
        var result = new PagedResult<AuditListItem>(items, total, pageIndex, pageSize);
        return Task.FromResult(result);
    }
}