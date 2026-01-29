using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models;
using Atlas.WorkflowCore.Exceptions;

namespace Atlas.WorkflowCore.Primitives;

/// <summary>
/// When 步骤 - 结果分支容器
/// </summary>
public class When : ContainerStepBody
{
    /// <summary>
    /// 期望的结果值
    /// </summary>
    public object? ExpectedOutcome { get; set; }

    public override ExecutionResult Run(IStepExecutionContext context)
    {
        // 获取父级 OutcomeSwitch 的结果值
        var switchOutcome = GetSwitchOutcome(context);

        // 比较期望值与实际值
        if (ExpectedOutcome != switchOutcome)
        {
            // 尝试字符串转换比较
            if (Convert.ToString(ExpectedOutcome) != Convert.ToString(switchOutcome))
            {
                // 不匹配，跳过此分支
                return ExecutionResult.Next();
            }
        }

        // 首次执行：创建分支并设置持久化数据
        if (context.PersistenceData == null)
        {
            var persistenceData = new ControlPersistenceData { ChildrenActive = true };
            return ExecutionResult.Branch(new List<object> { context.Item ?? new object() }, persistenceData);
        }

        // 检查持久化数据类型
        if (context.PersistenceData is ControlPersistenceData controlData && controlData.ChildrenActive)
        {
            // 检查分支是否完成
            if (context.Workflow.IsBranchComplete(context.ExecutionPointer.Id))
            {
                // 分支完成，继续执行
                return ExecutionResult.Next();
            }
            else
            {
                // 分支未完成，保持等待状态
                return ExecutionResult.Persist(context.PersistenceData);
            }
        }

        // 持久化数据损坏
        throw new CorruptPersistenceDataException("Invalid persistence data for When");
    }

    /// <summary>
    /// 获取父级 OutcomeSwitch 的结果值
    /// </summary>
    private object? GetSwitchOutcome(IStepExecutionContext context)
    {
        // 查找包含当前指针的父级 OutcomeSwitch 指针
        var switchPointer = context.Workflow.ExecutionPointers
            .FirstOrDefault(x => x.Children.Contains(context.ExecutionPointer.Id));

        return switchPointer?.Outcome;
    }
}
