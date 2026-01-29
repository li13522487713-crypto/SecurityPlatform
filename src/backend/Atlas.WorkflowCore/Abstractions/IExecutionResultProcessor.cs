using System;
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
    /// <param name="def">工作流定义</param>
    /// <param name="pointer">执行指针</param>
    /// <param name="step">步骤定义</param>
    /// <param name="result">执行结果</param>
    /// <param name="workflowResult">工作流执行器结果（收集订阅和错误）</param>
    void ProcessExecutionResult(
        WorkflowInstance workflow,
        WorkflowDefinition def,
        ExecutionPointer pointer,
        WorkflowStep step,
        ExecutionResult result,
        WorkflowExecutorResult workflowResult);

    /// <summary>
    /// 处理步骤执行异常
    /// </summary>
    /// <param name="workflow">工作流实例</param>
    /// <param name="def">工作流定义</param>
    /// <param name="pointer">执行指针</param>
    /// <param name="step">步骤定义</param>
    /// <param name="exception">异常</param>
    void HandleStepException(
        WorkflowInstance workflow,
        WorkflowDefinition def,
        ExecutionPointer pointer,
        WorkflowStep step,
        Exception exception);
}
