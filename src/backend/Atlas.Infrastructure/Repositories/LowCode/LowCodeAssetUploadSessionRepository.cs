using Atlas.Application.LowCode.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories.LowCode;

public sealed class LowCodeAssetUploadSessionRepository : ILowCodeAssetUploadSessionRepository
{
    private readonly ISqlSugarClient _db;
    public LowCodeAssetUploadSessionRepository(ISqlSugarClient db) => _db = db;

    public async Task<long> InsertAsync(LowCodeAssetUploadSession session, CancellationToken cancellationToken)
    {
        await _db.Insertable(session).ExecuteCommandAsync(cancellationToken);
        return session.Id;
    }

    public Task<LowCodeAssetUploadSession?> FindByTokenAsync(TenantId tenantId, string token, CancellationToken cancellationToken)
    {
        return _db.Queryable<LowCodeAssetUploadSession>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Token == token)
            .FirstAsync(cancellationToken)!;
    }

    public async Task<bool> UpdateAsync(LowCodeAssetUploadSession session, CancellationToken cancellationToken)
    {
        var rows = await _db.Updateable(session)
            .Where(x => x.Id == session.Id && x.TenantIdValue == session.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
        return rows > 0;
    }

    public async Task<int> ExpireOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken)
    {
        // 抓取受影响行 → 内存修改 → 批量 UpdateColumns 单条 SQL；
        // 复用聚合 MarkExpired 行为（领域不变性），并保持 AGENTS.md "禁止循环 DB" 约束。
        var rows = await _db.Queryable<LowCodeAssetUploadSession>()
            .Where(x => x.Status == "pending" && x.ExpiresAt < cutoffUtc)
            .ToListAsync(cancellationToken);
        if (rows.Count == 0) return 0;
        foreach (var r in rows) r.MarkExpired();
        var updated = await _db.Updateable(rows)
            .UpdateColumns(it => new { it.Status, it.CompletedAt })
            .ExecuteCommandAsync(cancellationToken);
        return updated;
    }
}
