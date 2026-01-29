using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models;
using Atlas.WorkflowCore.Exceptions;

namespace Atlas.WorkflowCore.Primitives;

/// <summary>
/// 结果开关步骤 - 多结果分支选择
/// </summary>
public class OutcomeSwitch : ContainerStepBody
{
    public override ExecutionResult Run(IStepExecutionContext context)
    {
        // 首次执行：创建分支并设置持久化数据
        if (context.PersistenceData == null)
        {
            var persistenceData = new ControlPersistenceData { ChildrenActive = true };
            var result = ExecutionResult.Branch(new List<object> { context.Item ?? new object() }, persistenceData);
            result.OutcomeValue = GetPreviousOutcome(context);
            return result;
        }

        // 检查持久化数据类型
        if (context.PersistenceData is ControlPersistenceData controlData && controlData.ChildrenActive)
        {
            // 检查分支是否完成
            if (context.Workflow.IsBranchComplete(context.ExecutionPointer.Id))
            {
                // 分支完成，继续执行
                var result = ExecutionResult.Next();
                result.OutcomeValue = GetPreviousOutcome(context);
                return result;
            }
            else
            {
                // 分支未完成，保持等待状态
                var result = ExecutionResult.Persist(context.PersistenceData);
                result.OutcomeValue = GetPreviousOutcome(context);
                return result;
            }
        }

        // 持久化数据损坏
        throw new CorruptPersistenceDataException("Invalid persistence data for OutcomeSwitch");
    }

    /// <summary>
    /// 获取前驱步骤的结果值
    /// </summary>
    private object? GetPreviousOutcome(IStepExecutionContext context)
    {
        if (string.IsNullOrEmpty(context.ExecutionPointer.PredecessorId))
        {
            return null;
        }

        var prevPointer = context.Workflow.ExecutionPointers.FindById(context.ExecutionPointer.PredecessorId);
        return prevPointer?.Outcome;
    }
}
