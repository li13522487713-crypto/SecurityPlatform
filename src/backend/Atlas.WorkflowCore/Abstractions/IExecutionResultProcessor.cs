using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Abstractions;

/// <summary>
/// 执行结果处理器接口
/// </summary>
public interface IExecutionResultProcessor
{
    /// <summary>
    /// 处理步骤执行结果
    /// </summary>
    /// <param name="workflow">工作流实例</param>
    /// <param name="definition">工作流定义</param>
    /// <param name="pointer">执行指针</param>
    /// <param name="step">步骤定义</param>
    /// <param name="result">执行结果</param>
    /// <param name="workflowResult">工作流执行器结果（收集订阅和错误）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task ProcessExecutionResult(
        WorkflowInstance workflow,
        WorkflowDefinition definition,
        ExecutionPointer pointer,
        WorkflowStep step,
        ExecutionResult result,
        WorkflowExecutorResult workflowResult,
        CancellationToken cancellationToken);

    /// <summary>
    /// 处理步骤执行异常
    /// </summary>
    /// <param name="workflow">工作流实例</param>
    /// <param name="definition">工作流定义</param>
    /// <param name="pointer">执行指针</param>
    /// <param name="step">步骤定义</param>
    /// <param name="exception">异常</param>
    Task HandleStepException(
        WorkflowInstance workflow,
        WorkflowDefinition definition,
        ExecutionPointer pointer,
        WorkflowStep step,
        Exception exception);
}
