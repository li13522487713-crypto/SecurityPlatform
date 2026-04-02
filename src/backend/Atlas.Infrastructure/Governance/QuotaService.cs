using Atlas.Core.Governance;
using Atlas.Domain.LogicFlow.Governance;
using SqlSugar;

namespace Atlas.Infrastructure.Governance;

public sealed class QuotaService : IQuotaService
{
    private readonly ISqlSugarClient _db;

    public QuotaService(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<QuotaInfo> GetQuotaAsync(string tenantId, string resourceType, CancellationToken ct)
    {
        if (!Guid.TryParse(tenantId, out var tid))
            return new QuotaInfo(resourceType, 0, 0, 0);
        var row = await _db.Queryable<SysQuota>()
            .Where(x => x.TenantIdValue == tid && x.ResourceType == resourceType)
            .FirstAsync(ct);
        if (row is null)
            return new QuotaInfo(resourceType, 0, 0, 0);
        var remaining = Math.Max(0, row.Limit - row.Used);
        return new QuotaInfo(resourceType, row.Limit, row.Used, remaining);
    }

    public async Task<IReadOnlyList<QuotaInfo>> ListQuotasAsync(string tenantId, CancellationToken ct)
    {
        if (!Guid.TryParse(tenantId, out var tid))
            return [];
        var rows = await _db.Queryable<SysQuota>().Where(x => x.TenantIdValue == tid).OrderBy(x => x.ResourceType).ToListAsync(ct);
        return rows.Select(x => new QuotaInfo(x.ResourceType, x.Limit, x.Used, Math.Max(0, x.Limit - x.Used))).ToList();
    }

    public async Task<bool> TryConsumeAsync(string tenantId, string resourceType, int amount, CancellationToken ct)
    {
        if (amount <= 0 || !Guid.TryParse(tenantId, out var tid))
            return false;
        try
        {
            await _db.Ado.BeginTranAsync();
            var row = await _db.Queryable<SysQuota>()
                .Where(x => x.TenantIdValue == tid && x.ResourceType == resourceType)
                .FirstAsync(ct);
            if (row is null)
            {
                await _db.Ado.RollbackTranAsync();
                return false;
            }

            if (row.Used + amount > row.Limit)
            {
                await _db.Ado.RollbackTranAsync();
                return false;
            }

            row.Used += amount;
            await _db.Updateable(row).ExecuteCommandAsync(ct);
            await _db.Ado.CommitTranAsync();
            return true;
        }
        catch
        {
            await _db.Ado.RollbackTranAsync();
            throw;
        }
    }

    public async Task ResetAsync(string tenantId, string resourceType, CancellationToken ct)
    {
        if (!Guid.TryParse(tenantId, out var tid))
            return;
        await _db.Updateable<SysQuota>()
            .SetColumns(x => new SysQuota { Used = 0 })
            .Where(x => x.TenantIdValue == tid && x.ResourceType == resourceType)
            .ExecuteCommandAsync(ct);
    }
}
