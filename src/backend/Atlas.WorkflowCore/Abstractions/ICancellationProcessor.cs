using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Abstractions;

/// <summary>
/// 取消处理器接口
/// </summary>
public interface ICancellationProcessor
{
    /// <summary>
    /// 检查并处理取消条件
    /// </summary>
    /// <param name="workflow">工作流实例</param>
    /// <param name="definition">工作流定义</param>
    /// <param name="pointer">执行指针</param>
    /// <param name="step">步骤定义</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>如果步骤被取消返回true</returns>
    Task<bool> ProcessCancellation(
        WorkflowInstance workflow,
        WorkflowDefinition definition,
        ExecutionPointer pointer,
        WorkflowStep step,
        CancellationToken cancellationToken);
}
