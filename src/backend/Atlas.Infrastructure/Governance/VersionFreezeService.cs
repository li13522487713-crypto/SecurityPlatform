using Atlas.Core.Abstractions;
using Atlas.Core.Governance;
using Atlas.Core.Tenancy;
using Atlas.Domain.LogicFlow.Governance;
using SqlSugar;

namespace Atlas.Infrastructure.Governance;

public sealed class VersionFreezeService : IVersionFreezeService
{
    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly ITenantProvider _tenantProvider;

    public VersionFreezeService(ISqlSugarClient db, IIdGeneratorAccessor idGen, ITenantProvider tenantProvider)
    {
        _db = db;
        _idGen = idGen;
        _tenantProvider = tenantProvider;
    }

    public async Task<bool> IsFrozenAsync(string resourceType, long resourceId, CancellationToken ct)
    {
        var tenantValue = _tenantProvider.GetTenantId().Value;
        return await _db.Queryable<SysVersionFreeze>()
            .AnyAsync(x => x.TenantIdValue == tenantValue && x.ResourceType == resourceType && x.ResourceId == resourceId, ct);
    }

    public async Task FreezeAsync(string resourceType, long resourceId, string reason, string userId, CancellationToken ct)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var exists = await IsFrozenAsync(resourceType, resourceId, ct);
        if (exists)
            return;
        var row = new SysVersionFreeze(
            tenantId,
            resourceType,
            resourceId,
            reason,
            userId,
            DateTime.UtcNow)
        {
            Id = _idGen.Generator.NextId()
        };
        await _db.Insertable(row).ExecuteCommandAsync(ct);
    }

    public async Task UnfreezeAsync(string resourceType, long resourceId, string userId, CancellationToken ct)
    {
        var tenantValue = _tenantProvider.GetTenantId().Value;
        await _db.Deleteable<SysVersionFreeze>()
            .Where(x => x.TenantIdValue == tenantValue && x.ResourceType == resourceType && x.ResourceId == resourceId)
            .ExecuteCommandAsync(ct);
    }

    public async Task<VersionFreezeInfo?> GetFreezeInfoAsync(string resourceType, long resourceId, CancellationToken ct)
    {
        var tenantValue = _tenantProvider.GetTenantId().Value;
        var row = await _db.Queryable<SysVersionFreeze>()
            .Where(x => x.TenantIdValue == tenantValue && x.ResourceType == resourceType && x.ResourceId == resourceId)
            .FirstAsync(ct);
        if (row is null)
            return null;
        return new VersionFreezeInfo(row.ResourceType, row.ResourceId, row.Reason ?? string.Empty, row.FrozenBy, row.FrozenAt);
    }

    public async Task<IReadOnlyList<VersionFreezeInfo>> QueryFreezesAsync(string? resourceType, long? resourceId, CancellationToken ct)
    {
        var tenantValue = _tenantProvider.GetTenantId().Value;
        var q = _db.Queryable<SysVersionFreeze>().Where(x => x.TenantIdValue == tenantValue);
        if (!string.IsNullOrWhiteSpace(resourceType))
            q = q.Where(x => x.ResourceType == resourceType);
        if (resourceId.HasValue)
            q = q.Where(x => x.ResourceId == resourceId.Value);
        var rows = await q.OrderByDescending(x => x.FrozenAt).ToListAsync(ct);
        return rows.Select(x => new VersionFreezeInfo(x.ResourceType, x.ResourceId, x.Reason ?? string.Empty, x.FrozenBy, x.FrozenAt)).ToList();
    }
}
