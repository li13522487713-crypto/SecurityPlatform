using Atlas.Application.LowCode.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories.LowCode;

public sealed class LowCodeTriggerRepository : ILowCodeTriggerRepository
{
    private readonly ISqlSugarClient _db;
    public LowCodeTriggerRepository(ISqlSugarClient db) => _db = db;

    public async Task<long> InsertAsync(LowCodeTrigger trigger, CancellationToken cancellationToken)
    {
        await _db.Insertable(trigger).ExecuteCommandAsync(cancellationToken);
        return trigger.Id;
    }
    public async Task<bool> UpdateAsync(LowCodeTrigger trigger, CancellationToken cancellationToken)
    {
        var rows = await _db.Updateable(trigger).Where(x => x.Id == trigger.Id && x.TenantIdValue == trigger.TenantIdValue).ExecuteCommandAsync(cancellationToken);
        return rows > 0;
    }
    public Task<LowCodeTrigger?> FindByTriggerIdAsync(TenantId tenantId, string triggerId, CancellationToken cancellationToken)
        => _db.Queryable<LowCodeTrigger>().Where(x => x.TenantIdValue == tenantId.Value && x.TriggerId == triggerId).FirstAsync(cancellationToken)!;
    public async Task<IReadOnlyList<LowCodeTrigger>> ListAsync(TenantId tenantId, CancellationToken cancellationToken)
        => await _db.Queryable<LowCodeTrigger>().Where(x => x.TenantIdValue == tenantId.Value).OrderBy(x => x.UpdatedAt, OrderByType.Desc).ToListAsync(cancellationToken);
    public async Task<bool> DeleteAsync(TenantId tenantId, string triggerId, CancellationToken cancellationToken)
    {
        var rows = await _db.Deleteable<LowCodeTrigger>().Where(x => x.TenantIdValue == tenantId.Value && x.TriggerId == triggerId).ExecuteCommandAsync(cancellationToken);
        return rows > 0;
    }
}

public sealed class LowCodeWebviewDomainRepository : ILowCodeWebviewDomainRepository
{
    private readonly ISqlSugarClient _db;
    public LowCodeWebviewDomainRepository(ISqlSugarClient db) => _db = db;

    public async Task<long> InsertAsync(LowCodeWebviewDomain domain, CancellationToken cancellationToken)
    {
        await _db.Insertable(domain).ExecuteCommandAsync(cancellationToken);
        return domain.Id;
    }
    public async Task<bool> UpdateAsync(LowCodeWebviewDomain domain, CancellationToken cancellationToken)
    {
        var rows = await _db.Updateable(domain).Where(x => x.Id == domain.Id && x.TenantIdValue == domain.TenantIdValue).ExecuteCommandAsync(cancellationToken);
        return rows > 0;
    }
    public Task<LowCodeWebviewDomain?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
        => _db.Queryable<LowCodeWebviewDomain>().Where(x => x.TenantIdValue == tenantId.Value && x.Id == id).FirstAsync(cancellationToken)!;
    public Task<LowCodeWebviewDomain?> FindByDomainAsync(TenantId tenantId, string domain, CancellationToken cancellationToken)
        => _db.Queryable<LowCodeWebviewDomain>().Where(x => x.TenantIdValue == tenantId.Value && x.Domain == domain).FirstAsync(cancellationToken)!;
    public async Task<IReadOnlyList<LowCodeWebviewDomain>> ListAsync(TenantId tenantId, CancellationToken cancellationToken)
        => await _db.Queryable<LowCodeWebviewDomain>().Where(x => x.TenantIdValue == tenantId.Value).OrderBy(x => x.CreatedAt, OrderByType.Desc).ToListAsync(cancellationToken);
    public async Task<bool> DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var rows = await _db.Deleteable<LowCodeWebviewDomain>().Where(x => x.TenantIdValue == tenantId.Value && x.Id == id).ExecuteCommandAsync(cancellationToken);
        return rows > 0;
    }
}
