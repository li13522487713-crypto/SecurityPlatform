using Atlas.Application.LowCode.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories.LowCode;

public sealed class AppTemplateRepository : IAppTemplateRepository
{
    private readonly ISqlSugarClient _db;
    public AppTemplateRepository(ISqlSugarClient db) => _db = db;

    public async Task<long> InsertAsync(AppTemplate entity, CancellationToken cancellationToken)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return entity.Id;
    }
    public async Task<bool> UpdateAsync(AppTemplate entity, CancellationToken cancellationToken)
    {
        var rows = await _db.Updateable(entity).Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue).ExecuteCommandAsync(cancellationToken);
        return rows > 0;
    }
    public async Task<bool> DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var rows = await _db.Deleteable<AppTemplate>().Where(x => x.TenantIdValue == tenantId.Value && x.Id == id).ExecuteCommandAsync(cancellationToken);
        return rows > 0;
    }
    public Task<AppTemplate?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
        => _db.Queryable<AppTemplate>().Where(x => x.TenantIdValue == tenantId.Value && x.Id == id).FirstAsync(cancellationToken)!;
    public async Task<bool> ExistsCodeAsync(TenantId tenantId, string code, long? excludeId, CancellationToken cancellationToken)
    {
        var q = _db.Queryable<AppTemplate>().Where(x => x.TenantIdValue == tenantId.Value && x.Code == code);
        if (excludeId.HasValue) q = q.Where(x => x.Id != excludeId.Value);
        return await q.AnyAsync();
    }
    public async Task<IReadOnlyList<AppTemplate>> SearchAsync(TenantId tenantId, string? keyword, string? kind, string? shareScope, string? industryTag, int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        var q = _db.Queryable<AppTemplate>().Where(x => x.TenantIdValue == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(keyword)) q = q.Where(x => x.Code.Contains(keyword) || x.Name.Contains(keyword));
        if (!string.IsNullOrWhiteSpace(kind)) q = q.Where(x => x.Kind == kind);
        if (!string.IsNullOrWhiteSpace(shareScope)) q = q.Where(x => x.ShareScope == shareScope);
        if (!string.IsNullOrWhiteSpace(industryTag)) q = q.Where(x => x.IndustryTag == industryTag);
        return await q.OrderBy(x => x.Stars, OrderByType.Desc).OrderBy(x => x.UpdatedAt, OrderByType.Desc).ToPageListAsync(pageIndex, pageSize, cancellationToken);
    }
}
