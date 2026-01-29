using System;
using System.Collections.Generic;
using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Abstractions;

/// <summary>
/// 工作流错误处理器接口
/// </summary>
public interface IWorkflowErrorHandler
{
    /// <summary>
    /// 错误处理类型
    /// </summary>
    WorkflowErrorHandling Type { get; }

    /// <summary>
    /// 处理步骤异常
    /// </summary>
    /// <param name="workflow">工作流实例</param>
    /// <param name="def">工作流定义</param>
    /// <param name="pointer">执行指针</param>
    /// <param name="step">步骤定义</param>
    /// <param name="exception">异常</param>
    /// <param name="bubbleUpQueue">冒泡队列（用于补偿处理）</param>
    void Handle(
        WorkflowInstance workflow,
        WorkflowDefinition def,
        ExecutionPointer pointer,
        WorkflowStep step,
        Exception exception,
        Queue<ExecutionPointer> bubbleUpQueue);
}
