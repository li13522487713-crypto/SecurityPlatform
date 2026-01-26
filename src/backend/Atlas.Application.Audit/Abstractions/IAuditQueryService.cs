using Atlas.Application.Audit.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.Audit.Abstractions;

public interface IAuditQueryService
{
    PagedResult<AuditListItem> QueryAudits(PagedRequest request, TenantId tenantId);
}