using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Atlas.Infrastructure.Services.LowCode;

/// <summary>
/// 低代码 OTel 全链路 instrumentation（M13 收尾 / S13-3）。
///
/// 暴露：
///  - LowCodeActivitySource（lowcode.runtime）：用于 dispatch / workflow / chatflow / asset / state.patch 等 Activity（Span）
///  - LowCodeMeter（lowcode.runtime）：
///     * dispatch_latency        Histogram(ms) tags: tenantId / appId / status
///     * workflow_latency        Histogram(ms) tags: tenantId / workflowId / status
///     * error_count             Counter        tags: tenantId / source / errorKind
///     * circuit_state           UpDownCounter  tags: tenantId / workflowId / state
///     * chatflow_stream_chunk   Counter        tags: tenantId / chatflowId / chunkKind
///
/// 强约束（PLAN.md §M13 / docs/lowcode-resilience-spec.md §6）：
/// - 命名严格遵守 lowcode.* 前缀；标签命名使用驼峰；不写敏感字段。
/// </summary>
public static class LowCodeOtelInstrumentation
{
    public const string ActivitySourceName = "lowcode.runtime";
    public const string MeterName = "lowcode.runtime";
    public const string MeterVersion = "1.0.0";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    public static readonly Meter Meter = new(MeterName, MeterVersion);

    public static readonly Histogram<double> DispatchLatencyMs =
        Meter.CreateHistogram<double>("lowcode.dispatch_latency", unit: "ms", description: "dispatch 端到端耗时");

    public static readonly Histogram<double> WorkflowLatencyMs =
        Meter.CreateHistogram<double>("lowcode.workflow_latency", unit: "ms", description: "工作流单次调用耗时");

    public static readonly Counter<long> ErrorCount =
        Meter.CreateCounter<long>("lowcode.error_count", unit: "{event}", description: "错误事件总数");

    public static readonly UpDownCounter<long> CircuitState =
        Meter.CreateUpDownCounter<long>("lowcode.circuit_state", unit: "{state}", description: "熔断状态：0=closed,1=open");

    public static readonly Counter<long> ChatflowStreamChunk =
        Meter.CreateCounter<long>("lowcode.chatflow.stream_chunk", unit: "{chunk}", description: "chatflow SSE 帧计数");
}
