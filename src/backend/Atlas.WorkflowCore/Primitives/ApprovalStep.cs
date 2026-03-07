using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Primitives;

/// <summary>
/// 审批步骤：进入该步骤后创建等待事件订阅，直到收到审批完成事件再继续执行。
/// 事件由审批模块在实例完成/拒绝时发布。
/// </summary>
public sealed class ApprovalStep : StepBody
{
    public string EventName { get; set; } = "ApprovalDecision";

    public string EventKey { get; set; } = string.Empty;

    public DateTime EffectiveDate { get; set; }

    public object? EventData { get; private set; }

    public override ExecutionResult Run(IStepExecutionContext context)
    {
        if (!context.ExecutionPointer.EventPublished)
        {
            var effectiveDate = EffectiveDate != default ? EffectiveDate : DateTime.MinValue;
            return ExecutionResult.WaitForEvent(EventName, EventKey, effectiveDate);
        }

        EventData = context.ExecutionPointer.EventData;
        return ExecutionResult.Next();
    }
}
