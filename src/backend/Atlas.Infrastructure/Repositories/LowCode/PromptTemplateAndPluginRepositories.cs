using Atlas.Application.LowCode.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories.LowCode;

public sealed class PromptTemplateRepository : IPromptTemplateRepository
{
    private readonly ISqlSugarClient _db;
    public PromptTemplateRepository(ISqlSugarClient db) => _db = db;

    public async Task<long> InsertAsync(AppPromptTemplate entity, CancellationToken cancellationToken)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return entity.Id;
    }
    public async Task<bool> UpdateAsync(AppPromptTemplate entity, CancellationToken cancellationToken)
    {
        var rows = await _db.Updateable(entity).Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue).ExecuteCommandAsync(cancellationToken);
        return rows > 0;
    }
    public async Task<bool> DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var rows = await _db.Deleteable<AppPromptTemplate>().Where(x => x.TenantIdValue == tenantId.Value && x.Id == id).ExecuteCommandAsync(cancellationToken);
        return rows > 0;
    }
    public Task<AppPromptTemplate?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
        => _db.Queryable<AppPromptTemplate>().Where(x => x.TenantIdValue == tenantId.Value && x.Id == id).FirstAsync(cancellationToken)!;
    public async Task<bool> ExistsCodeAsync(TenantId tenantId, string code, long? excludeId, CancellationToken cancellationToken)
    {
        var q = _db.Queryable<AppPromptTemplate>().Where(x => x.TenantIdValue == tenantId.Value && x.Code == code);
        if (excludeId.HasValue) q = q.Where(x => x.Id != excludeId.Value);
        return await q.AnyAsync();
    }
    public async Task<IReadOnlyList<AppPromptTemplate>> SearchAsync(TenantId tenantId, string? keyword, int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        var q = _db.Queryable<AppPromptTemplate>().Where(x => x.TenantIdValue == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
            q = q.Where(x => x.Code.Contains(keyword) || x.Name.Contains(keyword) || x.Body.Contains(keyword));
        return await q.OrderBy(x => x.UpdatedAt, OrderByType.Desc).ToPageListAsync(pageIndex, pageSize, cancellationToken);
    }
}

public sealed class LowCodePluginRepository : ILowCodePluginRepository
{
    private readonly ISqlSugarClient _db;
    public LowCodePluginRepository(ISqlSugarClient db) => _db = db;

    public async Task<long> InsertDefAsync(LowCodePluginDefinition entity, CancellationToken cancellationToken)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return entity.Id;
    }
    public async Task<bool> UpdateDefAsync(LowCodePluginDefinition entity, CancellationToken cancellationToken)
    {
        var rows = await _db.Updateable(entity).Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue).ExecuteCommandAsync(cancellationToken);
        return rows > 0;
    }
    public async Task<bool> DeleteDefAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var rows = await _db.Deleteable<LowCodePluginDefinition>().Where(x => x.TenantIdValue == tenantId.Value && x.Id == id).ExecuteCommandAsync(cancellationToken);
        return rows > 0;
    }
    public Task<LowCodePluginDefinition?> FindDefByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
        => _db.Queryable<LowCodePluginDefinition>().Where(x => x.TenantIdValue == tenantId.Value && x.Id == id).FirstAsync(cancellationToken)!;
    public Task<LowCodePluginDefinition?> FindDefByPluginIdAsync(TenantId tenantId, string pluginId, CancellationToken cancellationToken)
        => _db.Queryable<LowCodePluginDefinition>().Where(x => x.TenantIdValue == tenantId.Value && x.PluginId == pluginId).FirstAsync(cancellationToken)!;
    public async Task<IReadOnlyList<LowCodePluginDefinition>> SearchDefsAsync(TenantId tenantId, string? keyword, string? shareScope, int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        var q = _db.Queryable<LowCodePluginDefinition>().Where(x => x.TenantIdValue == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(keyword)) q = q.Where(x => x.Name.Contains(keyword) || x.PluginId.Contains(keyword));
        if (!string.IsNullOrWhiteSpace(shareScope)) q = q.Where(x => x.ShareScope == shareScope);
        return await q.OrderBy(x => x.UpdatedAt, OrderByType.Desc).ToPageListAsync(pageIndex, pageSize, cancellationToken);
    }

    public async Task<long> InsertVersionAsync(LowCodePluginVersion entity, CancellationToken cancellationToken)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return entity.Id;
    }
    public async Task<long> InsertAuthAsync(LowCodePluginAuthorization entity, CancellationToken cancellationToken)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return entity.Id;
    }
    public Task<LowCodePluginAuthorization?> FindAuthAsync(TenantId tenantId, string pluginId, CancellationToken cancellationToken)
        => _db.Queryable<LowCodePluginAuthorization>().Where(x => x.TenantIdValue == tenantId.Value && x.PluginId == pluginId).OrderBy(x => x.GrantedAt, OrderByType.Desc).FirstAsync(cancellationToken)!;

    public Task<LowCodePluginUsage?> FindUsageAsync(TenantId tenantId, string pluginId, string day, CancellationToken cancellationToken)
        => _db.Queryable<LowCodePluginUsage>().Where(x => x.TenantIdValue == tenantId.Value && x.PluginId == pluginId && x.Day == day).FirstAsync(cancellationToken)!;
    public async Task<long> InsertUsageAsync(LowCodePluginUsage entity, CancellationToken cancellationToken)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return entity.Id;
    }
    public async Task<bool> UpdateUsageAsync(LowCodePluginUsage entity, CancellationToken cancellationToken)
    {
        var rows = await _db.Updateable(entity).Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue).ExecuteCommandAsync(cancellationToken);
        return rows > 0;
    }
}
