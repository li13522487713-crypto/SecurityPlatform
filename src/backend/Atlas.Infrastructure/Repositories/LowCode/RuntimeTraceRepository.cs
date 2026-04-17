using Atlas.Application.LowCode.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories.LowCode;

public sealed class RuntimeTraceRepository : IRuntimeTraceRepository
{
    private readonly ISqlSugarClient _db;
    public RuntimeTraceRepository(ISqlSugarClient db) => _db = db;

    public async Task<long> InsertTraceAsync(RuntimeTrace trace, CancellationToken cancellationToken)
    {
        await _db.Insertable(trace).ExecuteCommandAsync(cancellationToken);
        return trace.Id;
    }

    public async Task<bool> UpdateTraceAsync(RuntimeTrace trace, CancellationToken cancellationToken)
    {
        var rows = await _db.Updateable(trace).Where(x => x.Id == trace.Id && x.TenantIdValue == trace.TenantIdValue).ExecuteCommandAsync(cancellationToken);
        return rows > 0;
    }

    public async Task<int> InsertSpansBatchAsync(IReadOnlyList<RuntimeSpan> spans, CancellationToken cancellationToken)
    {
        if (spans.Count == 0) return 0;
        return await _db.Insertable(spans.ToList()).ExecuteCommandAsync(cancellationToken);
    }

    public Task<RuntimeTrace?> FindByTraceIdAsync(TenantId tenantId, string traceId, CancellationToken cancellationToken)
        => _db.Queryable<RuntimeTrace>().Where(x => x.TenantIdValue == tenantId.Value && x.TraceId == traceId).FirstAsync(cancellationToken)!;

    public async Task<IReadOnlyList<RuntimeSpan>> ListSpansByTraceAsync(TenantId tenantId, string traceId, CancellationToken cancellationToken)
        => await _db.Queryable<RuntimeSpan>().Where(x => x.TenantIdValue == tenantId.Value && x.TraceId == traceId).OrderBy(x => x.StartedAt, OrderByType.Asc).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RuntimeTrace>> QueryTracesAsync(TenantId tenantId, string? appId, string? pageId, string? componentId, DateTimeOffset? from, DateTimeOffset? to, string? errorKind, long? userId, int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        var q = _db.Queryable<RuntimeTrace>().Where(x => x.TenantIdValue == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(appId)) q = q.Where(x => x.AppId == appId);
        if (!string.IsNullOrWhiteSpace(pageId)) q = q.Where(x => x.PageId == pageId);
        if (!string.IsNullOrWhiteSpace(componentId)) q = q.Where(x => x.ComponentId == componentId);
        if (from.HasValue) q = q.Where(x => x.StartedAt >= from.Value);
        if (to.HasValue) q = q.Where(x => x.StartedAt <= to.Value);
        if (!string.IsNullOrWhiteSpace(errorKind)) q = q.Where(x => x.ErrorKind == errorKind);
        if (userId.HasValue) q = q.Where(x => x.UserId == userId.Value);
        return await q.OrderBy(x => x.StartedAt, OrderByType.Desc).ToPageListAsync(pageIndex, pageSize, cancellationToken);
    }
}
