using System.Diagnostics.Metrics;
using System.Collections.Generic;

namespace Atlas.Infrastructure.Observability;

internal static class AtlasMetrics
{
    private static readonly Meter Meter = new("Atlas.Infrastructure", "1.0.0");

    private static readonly Counter<long> ApprovalStartCounter =
        Meter.CreateCounter<long>("atlas.approval.start.count");

    private static readonly Histogram<double> ApprovalStartDurationMs =
        Meter.CreateHistogram<double>("atlas.approval.start.duration.ms", unit: "ms");

    private static readonly Counter<long> DynamicRecordQueryCounter =
        Meter.CreateCounter<long>("atlas.dynamic.query.count");

    private static readonly Histogram<double> DynamicRecordQueryDurationMs =
        Meter.CreateHistogram<double>("atlas.dynamic.query.duration.ms", unit: "ms");

    private static readonly Counter<long> TenantDatasourceResolveCounter =
        Meter.CreateCounter<long>("atlas.datasource.resolve.count");

    private static readonly Histogram<double> TenantDatasourceResolveDurationMs =
        Meter.CreateHistogram<double>("atlas.datasource.resolve.duration.ms", unit: "ms");

    public static void RecordApprovalStart(double elapsedMs, string status)
    {
        var tags = new[]
        {
            new KeyValuePair<string, object?>("status", status)
        };
        ApprovalStartCounter.Add(1, tags);
        ApprovalStartDurationMs.Record(elapsedMs, tags);
    }

    public static void RecordDynamicQuery(double elapsedMs, string status)
    {
        var tags = new[]
        {
            new KeyValuePair<string, object?>("status", status)
        };
        DynamicRecordQueryCounter.Add(1, tags);
        DynamicRecordQueryDurationMs.Record(elapsedMs, tags);
    }

    public static void RecordTenantDatasourceResolve(double elapsedMs, string status, string source)
    {
        var tags = new[]
        {
            new KeyValuePair<string, object?>("status", status),
            new KeyValuePair<string, object?>("source", source)
        };
        TenantDatasourceResolveCounter.Add(1, tags);
        TenantDatasourceResolveDurationMs.Record(elapsedMs, tags);
    }
}
