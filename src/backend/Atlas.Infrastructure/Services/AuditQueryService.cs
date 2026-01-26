using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Audit.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services;

public sealed class AuditQueryService : IAuditQueryService
{
    public PagedResult<AuditListItem> QueryAudits(PagedRequest request, TenantId tenantId)
    {
        var items = Array.Empty<AuditListItem>();
        return new PagedResult<AuditListItem>(items, 0, request.PageIndex, request.PageSize);
    }
}