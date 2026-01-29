using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Abstractions;

/// <summary>
/// 取消处理器接口
/// </summary>
public interface ICancellationProcessor
{
    /// <summary>
    /// 处理所有取消条件
    /// </summary>
    /// <param name="workflow">工作流实例</param>
    /// <param name="workflowDef">工作流定义</param>
    /// <param name="executionResult">执行结果</param>
    void ProcessCancellations(WorkflowInstance workflow, WorkflowDefinition workflowDef, WorkflowExecutorResult executionResult);
}
