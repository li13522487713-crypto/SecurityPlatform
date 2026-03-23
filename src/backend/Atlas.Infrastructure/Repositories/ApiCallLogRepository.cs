using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class ApiCallLogRepository : RepositoryBase<ApiCallLog>
{
    public ApiCallLogRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<OpenApiStatsAggregate> AggregateAsync(
        TenantId tenantId,
        long? projectId,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken)
    {
        var query = BuildQuery(tenantId, projectId, fromUtc, toUtc);
        var totalCalls = await query.CountAsync(cancellationToken);
        if (totalCalls <= 0)
        {
            return new OpenApiStatsAggregate(0, 0, 0D, 0);
        }

        var successCalls = await query.Clone()
            .Where(x => x.IsSuccess)
            .CountAsync(cancellationToken);
        var averageDurationMs = await query.Clone().AvgAsync(x => x.DurationMs);
        var maxDurationMs = await query.Clone().MaxAsync(x => x.DurationMs);
        return new OpenApiStatsAggregate(totalCalls, successCalls, averageDurationMs, maxDurationMs);
    }

    private ISugarQueryable<ApiCallLog> BuildQuery(
        TenantId tenantId,
        long? projectId,
        DateTime? fromUtc,
        DateTime? toUtc)
    {
        var query = Db.Queryable<ApiCallLog>()
            .Where(x => x.TenantIdValue == tenantId.Value);

        if (projectId.HasValue && projectId.Value > 0)
        {
            query = query.Where(x => x.ProjectId == projectId.Value);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAt <= toUtc.Value);
        }

        return query;
    }
}

public sealed record OpenApiStatsAggregate(
    long TotalCalls,
    long SuccessCalls,
    double AverageDurationMs,
    long MaxDurationMs);
