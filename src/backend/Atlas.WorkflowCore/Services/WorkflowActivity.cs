using System.Diagnostics;
using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Services;

/// <summary>
/// 工作流活动追踪 - OpenTelemetry 集成
/// </summary>
internal static class WorkflowActivityTracing
{
    private static readonly ActivitySource ActivitySource = new("WorkflowCore");

    /// <summary>
    /// 启动主机追踪活动
    /// </summary>
    internal static Activity? StartHost()
    {
        var activityName = "workflow start host";
        return ActivitySource.StartRootActivity(activityName, ActivityKind.Internal);
    }

    /// <summary>
    /// 启动消费追踪活动
    /// </summary>
    internal static Activity? StartConsume(QueueType queueType)
    {
        var activityName = $"workflow consume {GetQueueType(queueType)}";
        var activity = ActivitySource.StartRootActivity(activityName, ActivityKind.Consumer);

        activity?.SetTag("workflow.queue", queueType.ToString());

        return activity;
    }

    /// <summary>
    /// 启动轮询追踪活动
    /// </summary>
    internal static Activity? StartPoll(string type)
    {
        var activityName = $"workflow poll {type}";
        var activity = ActivitySource.StartRootActivity(activityName, ActivityKind.Client);

        activity?.SetTag("workflow.poll", type);

        return activity;
    }

    /// <summary>
    /// 丰富工作流追踪信息
    /// </summary>
    internal static void Enrich(WorkflowInstance workflow, string action)
    {
        var activity = Activity.Current;
        if (activity != null)
        {
            activity.DisplayName = $"workflow {action} {workflow.WorkflowDefinitionId}";
            activity.SetTag("workflow.id", workflow.Id);
            activity.SetTag("workflow.definition", workflow.WorkflowDefinitionId);
            activity.SetTag("workflow.status", workflow.Status.ToString());
        }
    }

    /// <summary>
    /// 丰富步骤追踪信息
    /// </summary>
    internal static void Enrich(WorkflowStep workflowStep)
    {
        var activity = Activity.Current;
        if (activity != null)
        {
            var stepName = string.IsNullOrEmpty(workflowStep.Name)
                ? "inline"
                : workflowStep.Name;

            if (string.IsNullOrEmpty(activity.DisplayName))
            {
                activity.DisplayName = $"step {stepName}";
            }
            else
            {
                activity.DisplayName += $" step {stepName}";
            }

            activity.SetTag("workflow.step.id", workflowStep.Id);
            activity.SetTag("workflow.step.name", stepName);
            activity.SetTag("workflow.step.type", workflowStep.BodyType?.Name ?? "unknown");
        }
    }

    /// <summary>
    /// 丰富执行结果追踪信息
    /// </summary>
    internal static void Enrich(WorkflowExecutorResult? result)
    {
        var activity = Activity.Current;
        if (activity != null)
        {
            activity.SetTag("workflow.subscriptions.count", result?.Subscriptions?.Count ?? 0);
            activity.SetTag("workflow.errors.count", result?.Errors?.Count ?? 0);

            if (result?.Errors?.Count > 0)
            {
                activity.SetStatus(ActivityStatusCode.Error);
            }
        }
    }

    /// <summary>
    /// 丰富事件追踪信息
    /// </summary>
    internal static void Enrich(Event evt)
    {
        var activity = Activity.Current;
        if (activity != null)
        {
            activity.DisplayName = $"workflow process {evt?.EventName}";
            activity.SetTag("workflow.event.id", evt?.Id ?? "");
            activity.SetTag("workflow.event.name", evt?.EventName ?? "");
            activity.SetTag("workflow.event.processed", evt?.IsProcessed ?? false);
        }
    }

    /// <summary>
    /// 丰富出队项追踪信息（扩展方法）
    /// </summary>
    internal static void EnrichWithDequeuedItem(this Activity? activity, string item)
    {
        if (activity != null)
        {
            activity.SetTag("workflow.queue.item", item);
        }
    }

    /// <summary>
    /// 启动根活动（清除当前活动上下文）
    /// </summary>
    private static Activity? StartRootActivity(
        this ActivitySource activitySource,
        string name,
        ActivityKind kind)
    {
        Activity.Current = null;
        return activitySource.StartActivity(name, kind);
    }

    /// <summary>
    /// 获取队列类型字符串
    /// </summary>
    private static string GetQueueType(QueueType queueType)
    {
        return queueType switch
        {
            QueueType.Workflow => "workflow",
            QueueType.Event => "event",
            QueueType.Index => "index",
            _ => "unknown"
        };
    }
}
