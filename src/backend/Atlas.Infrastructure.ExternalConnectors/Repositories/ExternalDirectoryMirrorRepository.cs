using Atlas.Application.ExternalConnectors.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.ExternalConnectors.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.ExternalConnectors.Repositories;

public sealed class ExternalDirectoryMirrorRepository : IExternalDirectoryMirrorRepository
{
    private readonly ISqlSugarClient _db;

    public ExternalDirectoryMirrorRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ExternalDepartmentMirror>> ListDepartmentsAsync(TenantId tenantId, long providerId, CancellationToken cancellationToken)
        => await _db.Queryable<ExternalDepartmentMirror>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ProviderId == providerId && !x.IsDeleted)
            .ToListAsync(cancellationToken);

    public async Task<ExternalDepartmentMirror?> GetDepartmentAsync(TenantId tenantId, long providerId, string externalDepartmentId, CancellationToken cancellationToken)
        => await _db.Queryable<ExternalDepartmentMirror>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ProviderId == providerId && x.ExternalDepartmentId == externalDepartmentId)
            .FirstAsync(cancellationToken);

    public async Task UpsertDepartmentAsync(ExternalDepartmentMirror entity, CancellationToken cancellationToken)
    {
        var existing = await _db.Queryable<ExternalDepartmentMirror>()
            .Where(x => x.TenantIdValue == entity.TenantIdValue && x.ProviderId == entity.ProviderId && x.ExternalDepartmentId == entity.ExternalDepartmentId)
            .FirstAsync(cancellationToken);
        if (existing is null)
        {
            await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            await _db.Updateable(entity)
                .Where(x => x.Id == existing.Id && x.TenantIdValue == entity.TenantIdValue)
                .ExecuteCommandAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<ExternalUserMirror>> ListUsersAsync(TenantId tenantId, long providerId, CancellationToken cancellationToken)
        => await _db.Queryable<ExternalUserMirror>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ProviderId == providerId && !x.IsDeleted)
            .ToListAsync(cancellationToken);

    public async Task<ExternalUserMirror?> GetUserAsync(TenantId tenantId, long providerId, string externalUserId, CancellationToken cancellationToken)
        => await _db.Queryable<ExternalUserMirror>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ProviderId == providerId && x.ExternalUserId == externalUserId)
            .FirstAsync(cancellationToken);

    public async Task UpsertUserAsync(ExternalUserMirror entity, CancellationToken cancellationToken)
    {
        var existing = await _db.Queryable<ExternalUserMirror>()
            .Where(x => x.TenantIdValue == entity.TenantIdValue && x.ProviderId == entity.ProviderId && x.ExternalUserId == entity.ExternalUserId)
            .FirstAsync(cancellationToken);
        if (existing is null)
        {
            await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            await _db.Updateable(entity)
                .Where(x => x.Id == existing.Id && x.TenantIdValue == entity.TenantIdValue)
                .ExecuteCommandAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<ExternalDepartmentUserRelation>> ListRelationsByDepartmentAsync(TenantId tenantId, long providerId, string externalDepartmentId, CancellationToken cancellationToken)
        => await _db.Queryable<ExternalDepartmentUserRelation>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ProviderId == providerId && x.ExternalDepartmentId == externalDepartmentId)
            .ToListAsync(cancellationToken);

    public async Task UpsertRelationAsync(ExternalDepartmentUserRelation entity, CancellationToken cancellationToken)
    {
        var existing = await _db.Queryable<ExternalDepartmentUserRelation>()
            .Where(x => x.TenantIdValue == entity.TenantIdValue
                && x.ProviderId == entity.ProviderId
                && x.ExternalDepartmentId == entity.ExternalDepartmentId
                && x.ExternalUserId == entity.ExternalUserId)
            .FirstAsync(cancellationToken);
        if (existing is null)
        {
            await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            await _db.Updateable(entity)
                .Where(x => x.Id == existing.Id && x.TenantIdValue == entity.TenantIdValue)
                .ExecuteCommandAsync(cancellationToken);
        }
    }

    public async Task DeleteRelationAsync(TenantId tenantId, long providerId, string externalDepartmentId, string externalUserId, CancellationToken cancellationToken)
        => await _db.Deleteable<ExternalDepartmentUserRelation>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.ProviderId == providerId
                && x.ExternalDepartmentId == externalDepartmentId
                && x.ExternalUserId == externalUserId)
            .ExecuteCommandAsync(cancellationToken);
}

public sealed class ExternalDirectorySyncJobRepository : IExternalDirectorySyncJobRepository
{
    private readonly ISqlSugarClient _db;

    public ExternalDirectorySyncJobRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task AddAsync(ExternalDirectorySyncJob job, CancellationToken cancellationToken)
        => await _db.Insertable(job).ExecuteCommandAsync(cancellationToken);

    public async Task UpdateAsync(ExternalDirectorySyncJob job, CancellationToken cancellationToken)
        => await _db.Updateable(job)
            .Where(x => x.Id == job.Id && x.TenantIdValue == job.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);

    public async Task<ExternalDirectorySyncJob?> GetByIdAsync(TenantId tenantId, long jobId, CancellationToken cancellationToken)
        => await _db.Queryable<ExternalDirectorySyncJob>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == jobId)
            .FirstAsync(cancellationToken);

    public async Task<IReadOnlyList<ExternalDirectorySyncJob>> ListRecentAsync(TenantId tenantId, long providerId, int take, CancellationToken cancellationToken)
        => await _db.Queryable<ExternalDirectorySyncJob>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ProviderId == providerId)
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .Take(take)
            .ToListAsync(cancellationToken);
}

public sealed class ExternalDirectorySyncDiffRepository : IExternalDirectorySyncDiffRepository
{
    private readonly ISqlSugarClient _db;

    public ExternalDirectorySyncDiffRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task AddAsync(ExternalDirectorySyncDiff diff, CancellationToken cancellationToken)
        => await _db.Insertable(diff).ExecuteCommandAsync(cancellationToken);

    public async Task<IReadOnlyList<ExternalDirectorySyncDiff>> ListByJobAsync(TenantId tenantId, long jobId, int skip, int take, CancellationToken cancellationToken)
        => await _db.Queryable<ExternalDirectorySyncDiff>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.JobId == jobId)
            .OrderBy(x => x.OccurredAt, OrderByType.Desc)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<int> CountByJobAsync(TenantId tenantId, long jobId, CancellationToken cancellationToken)
        => await _db.Queryable<ExternalDirectorySyncDiff>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.JobId == jobId)
            .CountAsync(cancellationToken);
}
