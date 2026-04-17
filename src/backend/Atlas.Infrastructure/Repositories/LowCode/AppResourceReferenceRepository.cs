using Atlas.Application.LowCode.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories.LowCode;

/// <summary>
/// 资源引用反查仓储（M01；M14 完整使用）。
/// 替换语义：删除该 app 下所有旧引用，再批量插入新引用——单事务两条 SQL，避免循环。
/// </summary>
public sealed class AppResourceReferenceRepository : IAppResourceReferenceRepository
{
    private readonly ISqlSugarClient _db;

    public AppResourceReferenceRepository(ISqlSugarClient db) => _db = db;

    public async Task<int> ReplaceForAppAsync(TenantId tenantId, long appId, IReadOnlyList<AppResourceReference> references, CancellationToken cancellationToken)
    {
        await _db.Deleteable<AppResourceReference>()
            .Where(x => x.AppId == appId && x.TenantIdValue == tenantId.Value)
            .ExecuteCommandAsync(cancellationToken);

        if (references.Count == 0) return 0;

        var inserted = await _db.Insertable(references.ToList()).ExecuteCommandAsync(cancellationToken);
        return inserted;
    }

    public async Task<IReadOnlyList<AppResourceReference>> ListByResourceAsync(TenantId tenantId, string resourceType, string resourceId, CancellationToken cancellationToken)
    {
        var list = await _db.Queryable<AppResourceReference>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ResourceType == resourceType && x.ResourceId == resourceId)
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .ToListAsync(cancellationToken);
        return list;
    }

    public async Task<IReadOnlyList<AppResourceReference>> ListByAppAsync(TenantId tenantId, long appId, CancellationToken cancellationToken)
    {
        var list = await _db.Queryable<AppResourceReference>()
            .Where(x => x.AppId == appId && x.TenantIdValue == tenantId.Value)
            .ToListAsync(cancellationToken);
        return list;
    }
}
