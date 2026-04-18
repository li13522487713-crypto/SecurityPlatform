using System.Diagnostics;
using Atlas.WorkflowCore.Models;
using Microsoft.Extensions.Logging;

namespace Atlas.WorkflowCore.Services;

/// <summary>
/// 工作流追踪 - System.Diagnostics.Activity 集成。
/// </summary>
/// <remarks>
/// 已在 OBS-145（v1.5）启用 Activity.SetTag：
/// - 无需引入 OpenTelemetry NuGet 包；System.Diagnostics 是 BCL
/// - Activity.Current 为 null 时 SetTag 安全跳过；上层 OTel pipeline（AppHost / PlatformHost
///   AddSource("lowcode.runtime") 之外的 source）若需采集，需在 pipeline 中 AddSource("Atlas.WorkflowCore")。
/// </remarks>
public static class WorkflowTracing
{
    /// <summary>WorkflowCore 自身 ActivitySource 名（上游 OTel pipeline 可 AddSource 接入）。</summary>
    public const string ActivitySourceName = "Atlas.WorkflowCore";

    private static readonly ActivitySource Source = new(ActivitySourceName, "1.5.0");
    private static ILogger? _logger;

    /// <summary>
    /// 初始化追踪活动
    /// </summary>
    public static void Initialize(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 启动主机追踪
    /// </summary>
    public static void StartHost()
    {
        _logger?.LogDebug("[Tracing] WorkflowHost started");
        Source.StartActivity("WorkflowHost.Start", ActivityKind.Internal)?.Dispose();
    }

    /// <summary>
    /// 丰富工作流追踪信息
    /// </summary>
    public static void Enrich(WorkflowInstance workflow)
    {
        _logger?.LogDebug("[Tracing] Workflow {WorkflowId} enriched", workflow.Id);
        var a = Activity.Current;
        if (a is null) return;
        a.SetTag("workflow.id", workflow.Id);
        a.SetTag("workflow.definition", workflow.WorkflowDefinitionId);
        a.SetTag("workflow.version", workflow.Version);
        a.SetTag("workflow.status", workflow.Status);
    }

    /// <summary>
    /// 丰富步骤追踪信息
    /// </summary>
    public static void Enrich(WorkflowStep step)
    {
        _logger?.LogDebug("[Tracing] Step {StepName} enriched", step.Name);
        var a = Activity.Current;
        if (a is null) return;
        a.SetTag("step.id", step.Id);
        a.SetTag("step.name", step.Name);
        a.SetTag("step.type", step.BodyType.Name);
    }

    /// <summary>
    /// 丰富执行结果追踪信息
    /// </summary>
    public static void Enrich(ExecutionResult result)
    {
        _logger?.LogDebug("[Tracing] ExecutionResult enriched - Proceed: {Proceed}", result.Proceed);
        var a = Activity.Current;
        if (a is null) return;
        a.SetTag("result.proceed", result.Proceed);
        a.SetTag("result.outcome", result.OutcomeValue);
    }

    /// <summary>
    /// 丰富执行器结果追踪信息
    /// </summary>
    public static void Enrich(WorkflowExecutorResult result)
    {
        _logger?.LogDebug("[Tracing] WorkflowExecutorResult enriched - Errors: {ErrorCount}, Subscriptions: {SubscriptionCount}", 
            result.Errors.Count, result.Subscriptions.Count);
        var a = Activity.Current;
        if (a is null) return;
        a.SetTag("result.errors.count", result.Errors.Count);
        a.SetTag("result.subscriptions.count", result.Subscriptions.Count);
    }
}
