using Atlas.Application.LowCode.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories.LowCode;

public sealed class AppPublishArtifactRepository : IAppPublishArtifactRepository
{
    private readonly ISqlSugarClient _db;

    public AppPublishArtifactRepository(ISqlSugarClient db) => _db = db;

    public async Task<long> InsertAsync(AppPublishArtifact artifact, CancellationToken cancellationToken)
    {
        await _db.Insertable(artifact).ExecuteCommandAsync(cancellationToken);
        return artifact.Id;
    }

    public async Task<bool> UpdateAsync(AppPublishArtifact artifact, CancellationToken cancellationToken)
    {
        var rows = await _db.Updateable(artifact)
            .Where(x => x.Id == artifact.Id && x.TenantIdValue == artifact.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
        return rows > 0;
    }

    public Task<AppPublishArtifact?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        return _db.Queryable<AppPublishArtifact>()
            .Where(x => x.Id == id && x.TenantIdValue == tenantId.Value)
            .FirstAsync(cancellationToken)!;
    }

    public async Task<IReadOnlyList<AppPublishArtifact>> ListByAppAsync(TenantId tenantId, long appId, CancellationToken cancellationToken)
    {
        var list = await _db.Queryable<AppPublishArtifact>()
            .Where(x => x.AppId == appId && x.TenantIdValue == tenantId.Value)
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .ToListAsync(cancellationToken);
        return list;
    }
}
