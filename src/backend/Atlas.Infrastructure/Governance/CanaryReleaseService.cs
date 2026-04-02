using System.Security.Cryptography;
using System.Text;
using Atlas.Core.Abstractions;
using Atlas.Core.Governance;
using Atlas.Domain.LogicFlow.Governance;
using SqlSugar;

namespace Atlas.Infrastructure.Governance;

public sealed class CanaryReleaseService : ICanaryReleaseService
{
    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGen;

    public CanaryReleaseService(ISqlSugarClient db, IIdGeneratorAccessor idGen)
    {
        _db = db;
        _idGen = idGen;
    }

    public async Task<bool> IsEnabledForTenantAsync(string featureKey, string tenantId, CancellationToken ct)
    {
        var info = await GetInfoAsync(featureKey, ct);
        if (!info.IsActive || info.RolloutPercentage <= 0)
            return false;
        if (info.RolloutPercentage >= 100)
            return true;
        var bucket = StableBucketPercent(featureKey, tenantId);
        return bucket < info.RolloutPercentage;
    }

    public async Task SetRolloutPercentageAsync(string featureKey, int percentage, CancellationToken ct)
    {
        var p = Math.Clamp(percentage, 0, 100);
        var existing = await _db.Queryable<SysCanaryRelease>()
            .Where(x => x.FeatureKey == featureKey)
            .FirstAsync(ct);
        if (existing is null)
        {
            var row = new SysCanaryRelease
            {
                Id = _idGen.Generator.NextId(),
                FeatureKey = featureKey,
                RolloutPercentage = p,
                IsActive = p > 0,
                ActivatedAt = p > 0 ? DateTime.UtcNow : null
            };
            await _db.Insertable(row).ExecuteCommandAsync(ct);
            return;
        }

        existing.RolloutPercentage = p;
        existing.IsActive = p > 0;
        if (p > 0 && existing.ActivatedAt is null)
            existing.ActivatedAt = DateTime.UtcNow;
        await _db.Updateable(existing).ExecuteCommandAsync(ct);
    }

    public async Task<CanaryReleaseInfo> GetInfoAsync(string featureKey, CancellationToken ct)
    {
        var row = await _db.Queryable<SysCanaryRelease>()
            .Where(x => x.FeatureKey == featureKey)
            .FirstAsync(ct);
        if (row is null)
            return new CanaryReleaseInfo(featureKey, 0, false, null);
        return new CanaryReleaseInfo(row.FeatureKey, row.RolloutPercentage, row.IsActive, row.ActivatedAt);
    }

    public async Task<IReadOnlyList<CanaryReleaseInfo>> ListAllAsync(CancellationToken ct)
    {
        var rows = await _db.Queryable<SysCanaryRelease>().OrderBy(x => x.FeatureKey).ToListAsync(ct);
        return rows.Select(x => new CanaryReleaseInfo(x.FeatureKey, x.RolloutPercentage, x.IsActive, x.ActivatedAt)).ToList();
    }

    private static int StableBucketPercent(string featureKey, string tenantId)
    {
        var bytes = Encoding.UTF8.GetBytes(featureKey + "\0" + tenantId);
        var hash = SHA256.HashData(bytes);
        var v = BitConverter.ToUInt32(hash, 0);
        return (int)(v % 100);
    }
}
