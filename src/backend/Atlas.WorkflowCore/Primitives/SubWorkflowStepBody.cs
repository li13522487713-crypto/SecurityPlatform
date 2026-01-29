using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models;
using System.Text.Json;

namespace Atlas.WorkflowCore.Primitives;

/// <summary>
/// 子工作流步骤体 - 执行子工作流
/// </summary>
public class SubWorkflowStepBody : StepBody
{
    /// <summary>
    /// 子工作流ID
    /// </summary>
    public string ChildWorkflowId { get; set; } = string.Empty;

    /// <summary>
    /// 子工作流版本
    /// </summary>
    public int? ChildWorkflowVersion { get; set; }

    /// <summary>
    /// 子工作流数据
    /// </summary>
    public object? ChildWorkflowData { get; set; }

    public override ExecutionResult Run(IStepExecutionContext context)
    {
        // 检查是否已经启动子工作流
        string? childWorkflowInstanceId = null;
        if (context.PersistenceData is Dictionary<string, object> dict)
        {
            if (dict.TryGetValue("ChildWorkflowInstanceId", out var instanceId))
            {
                childWorkflowInstanceId = instanceId?.ToString();
            }
        }

        if (!string.IsNullOrEmpty(childWorkflowInstanceId))
        {
            // 子工作流已启动，等待工作流完成事件
            // ExecutionResultProcessor 会创建事件订阅，等待 WorkflowCompleted 事件
            return ExecutionResult.WaitForEvent(
                "WorkflowCompleted",
                childWorkflowInstanceId,
                DateTime.UtcNow);
        }

        // 首次执行：请求启动子工作流
        // ExecutionResultProcessor 检测到这个 PersistenceData 后会：
        // 1. 调用 IWorkflowController.StartWorkflowAsync 启动子工作流
        // 2. 将返回的实例ID添加到 PersistenceData["ChildWorkflowInstanceId"]
        // 3. 重新激活当前指针，让步骤再次执行（进入等待事件状态）
        var persistenceData = new Dictionary<string, object>
        {
            ["ChildWorkflowId"] = ChildWorkflowId,
            ["ChildWorkflowVersion"] = ChildWorkflowVersion ?? 0,
            ["ChildWorkflowData"] = ChildWorkflowData != null 
                ? JsonSerializer.Serialize(ChildWorkflowData) 
                : "null"
        };

        return ExecutionResult.Persist(persistenceData);
    }
}
