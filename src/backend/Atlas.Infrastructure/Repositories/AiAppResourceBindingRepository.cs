using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class AiAppResourceBindingRepository : RepositoryBase<AiAppResourceBinding>
{
    public AiAppResourceBindingRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<List<AiAppResourceBinding>> ListByAppAsync(
        TenantId tenantId,
        long appId,
        string? resourceType,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<AiAppResourceBinding>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId);
        if (!string.IsNullOrWhiteSpace(resourceType))
        {
            var normalized = resourceType.Trim().ToLowerInvariant();
            query = query.Where(x => x.ResourceType == normalized);
        }
        return await query
            .OrderBy(x => x.DisplayOrder)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<AiAppResourceBinding?> FindAsync(
        TenantId tenantId,
        long appId,
        string resourceType,
        long resourceId,
        CancellationToken cancellationToken)
    {
        var normalized = resourceType.Trim().ToLowerInvariant();
        return await Db.Queryable<AiAppResourceBinding>()
            .Where(x => x.TenantIdValue == tenantId.Value
                        && x.AppId == appId
                        && x.ResourceType == normalized
                        && x.ResourceId == resourceId)
            .FirstAsync(cancellationToken);
    }

    public async Task<int> CountByResourceAsync(
        TenantId tenantId,
        string resourceType,
        long resourceId,
        CancellationToken cancellationToken)
    {
        var normalized = resourceType.Trim().ToLowerInvariant();
        return await Db.Queryable<AiAppResourceBinding>()
            .Where(x => x.TenantIdValue == tenantId.Value
                        && x.ResourceType == normalized
                        && x.ResourceId == resourceId)
            .CountAsync(cancellationToken);
    }

    public async Task<int> CountByAppAndTypeAsync(
        TenantId tenantId,
        long appId,
        string resourceType,
        CancellationToken cancellationToken)
    {
        var normalized = resourceType.Trim().ToLowerInvariant();
        return await Db.Queryable<AiAppResourceBinding>()
            .Where(x => x.TenantIdValue == tenantId.Value
                        && x.AppId == appId
                        && x.ResourceType == normalized)
            .CountAsync(cancellationToken);
    }

    public async Task DeleteByAppAndResourceAsync(
        TenantId tenantId,
        long appId,
        string resourceType,
        long resourceId,
        CancellationToken cancellationToken)
    {
        var normalized = resourceType.Trim().ToLowerInvariant();
        await Db.Deleteable<AiAppResourceBinding>()
            .Where(x => x.TenantIdValue == tenantId.Value
                        && x.AppId == appId
                        && x.ResourceType == normalized
                        && x.ResourceId == resourceId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task DeleteByResourceAsync(
        TenantId tenantId,
        string resourceType,
        long resourceId,
        CancellationToken cancellationToken)
    {
        var normalized = resourceType.Trim().ToLowerInvariant();
        await Db.Deleteable<AiAppResourceBinding>()
            .Where(x => x.TenantIdValue == tenantId.Value
                        && x.ResourceType == normalized
                        && x.ResourceId == resourceId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task DeleteByAppAsync(
        TenantId tenantId,
        long appId,
        CancellationToken cancellationToken)
    {
        await Db.Deleteable<AiAppResourceBinding>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId)
            .ExecuteCommandAsync(cancellationToken);
    }
}
