using Atlas.Application.LowCode.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories.LowCode;

public sealed class LowCodeSessionRepository : ILowCodeSessionRepository
{
    private readonly ISqlSugarClient _db;
    public LowCodeSessionRepository(ISqlSugarClient db) => _db = db;

    public async Task<long> InsertAsync(LowCodeSession session, CancellationToken cancellationToken)
    {
        await _db.Insertable(session).ExecuteCommandAsync(cancellationToken);
        return session.Id;
    }

    public Task<LowCodeSession?> FindBySessionIdAsync(TenantId tenantId, string sessionId, CancellationToken cancellationToken)
    {
        return _db.Queryable<LowCodeSession>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.SessionId == sessionId)
            .FirstAsync(cancellationToken)!;
    }

    public async Task<bool> UpdateAsync(LowCodeSession session, CancellationToken cancellationToken)
    {
        var rows = await _db.Updateable(session)
            .Where(x => x.Id == session.Id && x.TenantIdValue == session.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
        return rows > 0;
    }

    public async Task<IReadOnlyList<LowCodeSession>> ListByUserAsync(TenantId tenantId, long userId, CancellationToken cancellationToken)
    {
        return await _db.Queryable<LowCodeSession>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.UserId == userId)
            .OrderBy(x => x.Pinned, OrderByType.Desc)
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ClearMessagesAsync(TenantId tenantId, string sessionId, CancellationToken cancellationToken)
    {
        // 单条 SQL 删除消息日志
        var rows = await _db.Deleteable<LowCodeMessageLogEntry>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.SessionId == sessionId)
            .ExecuteCommandAsync(cancellationToken);
        return rows >= 0;
    }
}

public sealed class LowCodeMessageLogRepository : ILowCodeMessageLogRepository
{
    private readonly ISqlSugarClient _db;
    public LowCodeMessageLogRepository(ISqlSugarClient db) => _db = db;

    public async Task<int> InsertAsync(LowCodeMessageLogEntry entry, CancellationToken cancellationToken)
    {
        return await _db.Insertable(entry).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<int> InsertBatchAsync(IReadOnlyList<LowCodeMessageLogEntry> entries, CancellationToken cancellationToken)
    {
        if (entries.Count == 0) return 0;
        return await _db.Insertable(entries.ToList()).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LowCodeMessageLogEntry>> QueryAsync(TenantId tenantId, string? sessionId, string? workflowId, string? agentId, DateTimeOffset? from, DateTimeOffset? to, int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        var q = _db.Queryable<LowCodeMessageLogEntry>().Where(x => x.TenantIdValue == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(sessionId)) q = q.Where(x => x.SessionId == sessionId);
        if (!string.IsNullOrWhiteSpace(workflowId)) q = q.Where(x => x.WorkflowId == workflowId);
        if (!string.IsNullOrWhiteSpace(agentId)) q = q.Where(x => x.AgentId == agentId);
        if (from.HasValue) q = q.Where(x => x.OccurredAt >= from.Value);
        if (to.HasValue) q = q.Where(x => x.OccurredAt <= to.Value);
        return await q.OrderBy(x => x.OccurredAt, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
    }
}
