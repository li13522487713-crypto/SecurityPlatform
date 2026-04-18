using Atlas.Application.LowCode.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories.LowCode;

/// <summary>应用定义仓储实现（M01）。</summary>
public sealed class AppDefinitionRepository : IAppDefinitionRepository
{
    private readonly ISqlSugarClient _db;

    public AppDefinitionRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<long> InsertAsync(AppDefinition app, CancellationToken cancellationToken)
    {
        await _db.Insertable(app).ExecuteCommandAsync(cancellationToken);
        return app.Id;
    }

    public async Task<bool> UpdateAsync(AppDefinition app, CancellationToken cancellationToken)
    {
        var rows = await _db.Updateable(app)
            .Where(x => x.Id == app.Id && x.TenantIdValue == app.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var rows = await _db.Deleteable<AppDefinition>()
            .Where(x => x.Id == id && x.TenantIdValue == tenantId.Value)
            .ExecuteCommandAsync(cancellationToken);
        return rows > 0;
    }

    public Task<AppDefinition?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        return _db.Queryable<AppDefinition>()
            .Where(x => x.Id == id && x.TenantIdValue == tenantId.Value)
            .FirstAsync(cancellationToken)!;
    }

    public Task<AppDefinition?> FindByCodeAsync(TenantId tenantId, string code, CancellationToken cancellationToken)
    {
        return _db.Queryable<AppDefinition>()
            .Where(x => x.Code == code && x.TenantIdValue == tenantId.Value)
            .FirstAsync(cancellationToken)!;
    }

    public async Task<bool> ExistsCodeAsync(TenantId tenantId, string code, long? excludeId, CancellationToken cancellationToken)
    {
        var q = _db.Queryable<AppDefinition>()
            .Where(x => x.Code == code && x.TenantIdValue == tenantId.Value);
        if (excludeId.HasValue)
        {
            q = q.Where(x => x.Id != excludeId.Value);
        }

        return await q.AnyAsync();
    }

    public async Task<(IReadOnlyList<AppDefinition> Items, int TotalCount)> QueryPagedAsync(
        TenantId tenantId,
        int pageIndex,
        int pageSize,
        string? keyword,
        string? status,
        CancellationToken cancellationToken)
    {
        var q = _db.Queryable<AppDefinition>()
            .Where(x => x.TenantIdValue == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            q = q.Where(x => x.Code.Contains(keyword) || x.DisplayName.Contains(keyword));
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            q = q.Where(x => x.Status == status);
        }

        var total = await q.CountAsync(cancellationToken);
        var list = await q.OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return (list, total);
    }
}
