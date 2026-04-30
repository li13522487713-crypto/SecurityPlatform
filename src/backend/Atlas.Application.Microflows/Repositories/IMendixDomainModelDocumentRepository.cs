using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;

namespace Atlas.Application.Microflows.Repositories;

public interface IMendixDomainModelDocumentRepository
{
    Task<MendixDomainModelDocument?> FindAsync(
        TenantId tenantId,
        long appId,
        string workspaceId,
        string moduleId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MendixDomainModelDocument>> ListByAppAsync(
        TenantId tenantId,
        long appId,
        string workspaceId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MendixDomainModelDocument>> ListByWorkspaceModuleAsync(
        TenantId tenantId,
        string workspaceId,
        string moduleId,
        CancellationToken cancellationToken);

    Task<long> InsertAsync(MendixDomainModelDocument document, CancellationToken cancellationToken);

    Task<bool> UpdateAsync(MendixDomainModelDocument document, CancellationToken cancellationToken);
}
