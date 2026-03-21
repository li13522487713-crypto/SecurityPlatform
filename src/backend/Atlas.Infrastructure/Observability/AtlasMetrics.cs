using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Collections.Generic;

namespace Atlas.Infrastructure.Observability;

internal static class AtlasMetrics
{
    public static readonly ActivitySource ActivitySource = new("Atlas.Infrastructure", "1.0.0");

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

    // 认证指标
    private static readonly Counter<long> AuthLoginCounter =
        Meter.CreateCounter<long>("atlas.auth.login.count", description: "用户登录次数");

    private static readonly Counter<long> AuthLoginFailCounter =
        Meter.CreateCounter<long>("atlas.auth.login.fail.count", description: "登录失败次数");

    // HTTP 请求指标
    private static readonly Counter<long> HttpRequestCounter =
        Meter.CreateCounter<long>("atlas.http.request.count", description: "HTTP 请求总数");

    private static readonly Counter<long> HttpErrorCounter =
        Meter.CreateCounter<long>("atlas.http.error.count", description: "HTTP 4xx/5xx 错误数");

    private static readonly Histogram<double> HttpRequestDurationMs =
        Meter.CreateHistogram<double>("atlas.http.request.duration.ms", unit: "ms", description: "HTTP 请求耗时");

    // CRUD 操作指标
    private static readonly Counter<long> CrudOperationCounter =
        Meter.CreateCounter<long>("atlas.crud.operation.count", description: "CRUD 操作次数");

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

    public static void RecordLogin(bool success, string? tenantId = null)
    {
        var tags = new[] { new KeyValuePair<string, object?>("tenant_id", tenantId ?? "unknown") };
        if (success)
        {
            AuthLoginCounter.Add(1, tags);
        }
        else
        {
            AuthLoginFailCounter.Add(1, tags);
        }
    }

    public static void RecordHttpRequest(string method, string path, int statusCode, double elapsedMs)
    {
        var tags = new[]
        {
            new KeyValuePair<string, object?>("method", method),
            new KeyValuePair<string, object?>("status_code", statusCode)
        };
        HttpRequestCounter.Add(1, tags);
        HttpRequestDurationMs.Record(elapsedMs, tags);
        if (statusCode >= 400)
        {
            HttpErrorCounter.Add(1, tags);
        }
    }

    public static void RecordCrudOperation(string entity, string operation, string status)
    {
        var tags = new[]
        {
            new KeyValuePair<string, object?>("entity", entity),
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("status", status)
        };
        CrudOperationCounter.Add(1, tags);
    }

    /// <summary>创建业务链路 Span</summary>
    public static Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
        => ActivitySource.StartActivity(name, kind);
}
