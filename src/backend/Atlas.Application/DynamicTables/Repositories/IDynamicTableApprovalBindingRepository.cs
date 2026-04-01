using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;

namespace Atlas.Application.DynamicTables.Repositories;

public interface IDynamicTableApprovalBindingRepository
{
    Task<DynamicTableApprovalBinding?> FindByTableKeyAsync(
        TenantId tenantId,
        string tableKey,
        CancellationToken cancellationToken);

    Task UpsertAsync(
        DynamicTableApprovalBinding binding,
        CancellationToken cancellationToken);
}
