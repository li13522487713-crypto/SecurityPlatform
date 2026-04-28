using Atlas.Core.Tenancy;
using Atlas.Domain.ExternalConnectors.Entities;

namespace Atlas.Application.ExternalConnectors.Repositories;

public interface IExternalDirectoryMirrorRepository
{
    Task<IReadOnlyList<ExternalDepartmentMirror>> ListDepartmentsAsync(TenantId tenantId, long providerId, CancellationToken cancellationToken);

    Task<ExternalDepartmentMirror?> GetDepartmentAsync(TenantId tenantId, long providerId, string externalDepartmentId, CancellationToken cancellationToken);

    Task UpsertDepartmentAsync(ExternalDepartmentMirror entity, CancellationToken cancellationToken);

    Task<IReadOnlyList<ExternalUserMirror>> ListUsersAsync(TenantId tenantId, long providerId, CancellationToken cancellationToken);

    Task<ExternalUserMirror?> GetUserAsync(TenantId tenantId, long providerId, string externalUserId, CancellationToken cancellationToken);

    Task UpsertUserAsync(ExternalUserMirror entity, CancellationToken cancellationToken);

    Task<IReadOnlyList<ExternalDepartmentUserRelation>> ListRelationsByDepartmentAsync(TenantId tenantId, long providerId, string externalDepartmentId, CancellationToken cancellationToken);

    Task UpsertRelationAsync(ExternalDepartmentUserRelation entity, CancellationToken cancellationToken);

    Task DeleteRelationAsync(TenantId tenantId, long providerId, string externalDepartmentId, string externalUserId, CancellationToken cancellationToken);
}

public interface IExternalDirectorySyncJobRepository
{
    Task AddAsync(ExternalDirectorySyncJob job, CancellationToken cancellationToken);

    Task UpdateAsync(ExternalDirectorySyncJob job, CancellationToken cancellationToken);

    Task<ExternalDirectorySyncJob?> GetByIdAsync(TenantId tenantId, long jobId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ExternalDirectorySyncJob>> ListRecentAsync(TenantId tenantId, long providerId, int take, CancellationToken cancellationToken);
}

public interface IExternalDirectorySyncDiffRepository
{
    Task AddAsync(ExternalDirectorySyncDiff diff, CancellationToken cancellationToken);

    Task<IReadOnlyList<ExternalDirectorySyncDiff>> ListByJobAsync(TenantId tenantId, long jobId, int skip, int take, CancellationToken cancellationToken);

    Task<int> CountByJobAsync(TenantId tenantId, long jobId, CancellationToken cancellationToken);
}
