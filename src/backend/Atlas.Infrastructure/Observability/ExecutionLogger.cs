using Atlas.Core.Abstractions;
using Atlas.Core.Observability;
using Atlas.Core.Tenancy;
using Atlas.Domain.LogicFlow.Flows;
using SqlSugar;

namespace Atlas.Infrastructure.Observability;

public sealed class ExecutionLogger : IExecutionLogger
{
    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly ITenantProvider _tenantProvider;

    public ExecutionLogger(ISqlSugarClient db, IIdGeneratorAccessor idGen, ITenantProvider tenantProvider)
    {
        _db = db;
        _idGen = idGen;
        _tenantProvider = tenantProvider;
    }

    public async Task LogAsync(ExecutionLogEntry entry, CancellationToken ct)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var entity = new LfExecutionLog(
            tenantId,
            entry.FlowExecutionId,
            entry.NodeKey,
            entry.Level,
            entry.Message,
            entry.StructuredDataJson,
            entry.Timestamp,
            entry.TraceId,
            entry.SpanId)
        {
            Id = _idGen.Generator.NextId()
        };
        await _db.Insertable(entity).ExecuteCommandAsync(ct);
    }

    public async Task<IReadOnlyList<ExecutionLogEntry>> QueryAsync(ExecutionLogQuery query, CancellationToken ct)
    {
        var tenantValue = _tenantProvider.GetTenantId().Value;
        var q = _db.Queryable<LfExecutionLog>().Where(x => x.TenantIdValue == tenantValue);
        if (query.FlowExecutionId.HasValue)
            q = q.Where(x => x.FlowExecutionId == query.FlowExecutionId.Value);
        if (!string.IsNullOrWhiteSpace(query.NodeKey))
            q = q.Where(x => x.NodeKey == query.NodeKey);
        if (!string.IsNullOrWhiteSpace(query.Level))
            q = q.Where(x => x.Level == query.Level);
        if (query.From.HasValue)
            q = q.Where(x => x.Timestamp >= query.From.Value);
        if (query.To.HasValue)
            q = q.Where(x => x.Timestamp <= query.To.Value);

        var pageIndex = Math.Max(1, query.PageIndex);
        var pageSize = Math.Clamp(query.PageSize, 1, 500);
        var rows = await q.OrderByDescending(x => x.Timestamp)
            .ToPageListAsync(pageIndex, pageSize, ct);
        return rows.Select(x => new ExecutionLogEntry(
            x.FlowExecutionId,
            x.NodeKey,
            x.Level,
            x.Message,
            x.StructuredDataJson,
            x.Timestamp,
            x.TraceId,
            x.SpanId)).ToList();
    }
}
