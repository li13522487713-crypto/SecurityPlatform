using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models;
using Atlas.WorkflowCore.Exceptions;

namespace Atlas.WorkflowCore.Primitives;

/// <summary>
/// 调度执行步骤 - 延迟后执行子步骤
/// </summary>
public class Schedule : ContainerStepBody
{
    /// <summary>
    /// 延迟时间
    /// </summary>
    public TimeSpan Interval { get; set; }

    public override ExecutionResult Run(IStepExecutionContext context)
    {
        // 首次执行：休眠指定时间
        if (context.PersistenceData == null)
        {
            var persistenceData = new SchedulePersistenceData { Elapsed = false };
            return ExecutionResult.Sleep(Interval, persistenceData);
        }

        // 检查持久化数据类型
        if (context.PersistenceData is SchedulePersistenceData scheduleData)
        {
            // 如果还未经过延迟，创建分支执行子步骤
            if (!scheduleData.Elapsed)
            {
                var newPersistenceData = new SchedulePersistenceData { Elapsed = true };
                return ExecutionResult.Branch(new List<object> { context.Item ?? new object() }, newPersistenceData);
            }

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
        throw new CorruptPersistenceDataException("Invalid persistence data for Schedule");
    }
}
