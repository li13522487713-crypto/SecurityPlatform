using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class WorkspaceRepository : RepositoryBase<Workspace>
{
    public WorkspaceRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public Task<List<Workspace>> ListByTenantAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        return Db.Queryable<Workspace>()
            .Where(x => x.TenantIdValue == tenantId.Value && !x.IsArchived)
            .OrderBy(x => x.LastVisitedAt, OrderByType.Desc)
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .ToListAsync(cancellationToken);
    }

    public Task<Workspace?> FindByAppKeyAsync(TenantId tenantId, string appKey, CancellationToken cancellationToken)
    {
        return Db.Queryable<Workspace>()
            .Where(x => x.TenantIdValue == tenantId.Value && !x.IsArchived && x.AppKey == appKey)
            .FirstAsync(cancellationToken)!;
    }

    public Task<Workspace?> FindByAppInstanceIdAsync(TenantId tenantId, long appInstanceId, CancellationToken cancellationToken)
    {
        return Db.Queryable<Workspace>()
            .Where(x => x.TenantIdValue == tenantId.Value && !x.IsArchived && x.AppInstanceId == appInstanceId)
            .FirstAsync(cancellationToken)!;
    }
}

public sealed class WorkspaceRoleRepository : RepositoryBase<WorkspaceRole>
{
    public WorkspaceRoleRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public Task<List<WorkspaceRole>> ListByWorkspaceAsync(TenantId tenantId, long workspaceId, CancellationToken cancellationToken)
    {
        return Db.Queryable<WorkspaceRole>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.WorkspaceId == workspaceId)
            .OrderBy(x => x.Id, OrderByType.Asc)
            .ToListAsync(cancellationToken);
    }

    public Task<WorkspaceRole?> FindByCodeAsync(TenantId tenantId, long workspaceId, string code, CancellationToken cancellationToken)
    {
        return Db.Queryable<WorkspaceRole>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.WorkspaceId == workspaceId && x.Code == code)
            .FirstAsync(cancellationToken)!;
    }
}

public sealed class WorkspaceMemberRepository : RepositoryBase<WorkspaceMember>
{
    public WorkspaceMemberRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public Task<List<WorkspaceMember>> ListByWorkspaceAsync(TenantId tenantId, long workspaceId, CancellationToken cancellationToken)
    {
        return Db.Queryable<WorkspaceMember>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.WorkspaceId == workspaceId)
            .ToListAsync(cancellationToken);
    }

    public Task<List<WorkspaceMember>> ListByUserAsync(TenantId tenantId, long userId, CancellationToken cancellationToken)
    {
        return Db.Queryable<WorkspaceMember>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public Task<WorkspaceMember?> FindByWorkspaceAndUserAsync(TenantId tenantId, long workspaceId, long userId, CancellationToken cancellationToken)
    {
        return Db.Queryable<WorkspaceMember>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.WorkspaceId == workspaceId && x.UserId == userId)
            .FirstAsync(cancellationToken)!;
    }

    public Task DeleteByWorkspaceAndUserAsync(TenantId tenantId, long workspaceId, long userId, CancellationToken cancellationToken)
    {
        return Db.Deleteable<WorkspaceMember>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.WorkspaceId == workspaceId && x.UserId == userId)
            .ExecuteCommandAsync(cancellationToken);
    }
}

public sealed class WorkspaceResourcePermissionRepository : RepositoryBase<WorkspaceResourcePermission>
{
    public WorkspaceResourcePermissionRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public Task<List<WorkspaceResourcePermission>> ListByResourceAsync(
        TenantId tenantId,
        long workspaceId,
        string resourceType,
        long resourceId,
        CancellationToken cancellationToken)
    {
        return Db.Queryable<WorkspaceResourcePermission>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value
                && x.WorkspaceId == workspaceId
                && x.ResourceType == resourceType
                && x.ResourceId == resourceId)
            .ToListAsync(cancellationToken);
    }

    public async Task ReplaceAsync(
        TenantId tenantId,
        long workspaceId,
        string resourceType,
        long resourceId,
        IReadOnlyList<WorkspaceResourcePermission> items,
        CancellationToken cancellationToken)
    {
        await Db.Deleteable<WorkspaceResourcePermission>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value
                && x.WorkspaceId == workspaceId
                && x.ResourceType == resourceType
                && x.ResourceId == resourceId)
            .ExecuteCommandAsync(cancellationToken);

        if (items.Count > 0)
        {
            await Db.Insertable(items.ToArray()).ExecuteCommandAsync(cancellationToken);
        }
    }
}
