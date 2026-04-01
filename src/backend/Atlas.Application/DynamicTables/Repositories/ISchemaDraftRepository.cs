using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;
using Atlas.Domain.DynamicTables.Enums;

namespace Atlas.Application.DynamicTables.Repositories;

public interface ISchemaDraftRepository
{
    Task<IReadOnlyList<SchemaDraft>> ListByAppInstanceAsync(
        TenantId tenantId,
        long appInstanceId,
        CancellationToken cancellationToken);

    Task<SchemaDraft?> FindByIdAsync(
        TenantId tenantId,
        long draftId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SchemaDraft>> ListPendingByAppAsync(
        TenantId tenantId,
        long appInstanceId,
        CancellationToken cancellationToken);

    Task AddAsync(SchemaDraft draft, CancellationToken cancellationToken);

    Task UpdateAsync(SchemaDraft draft, CancellationToken cancellationToken);

    Task UpdateRangeAsync(IReadOnlyList<SchemaDraft> drafts, CancellationToken cancellationToken);
}
