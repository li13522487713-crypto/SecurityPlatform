using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Abstractions;

/// <summary>
/// 执行指针工厂接口
/// </summary>
public interface IExecutionPointerFactory
{
    /// <summary>
    /// 构建初始执行指针（创世指针）
    /// </summary>
    /// <param name="step">步骤定义</param>
    /// <returns>执行指针</returns>
    ExecutionPointer BuildGenesisPointer(WorkflowStep step);

    /// <summary>
    /// 构建后续执行指针
    /// </summary>
    /// <param name="step">步骤定义</param>
    /// <param name="parentPointer">父执行指针</param>
    /// <returns>执行指针</returns>
    ExecutionPointer BuildNextPointer(WorkflowStep step, ExecutionPointer parentPointer);

    /// <summary>
    /// 构建子执行指针（用于容器步骤）
    /// </summary>
    /// <param name="step">步骤定义</param>
    /// <param name="parentPointer">父执行指针</param>
    /// <param name="scope">作用域</param>
    /// <returns>执行指针</returns>
    ExecutionPointer BuildChildPointer(WorkflowStep step, ExecutionPointer parentPointer, string scope);

    /// <summary>
    /// 构建补偿执行指针
    /// </summary>
    /// <param name="definition">工作流定义</param>
    /// <param name="parentPointer">父执行指针</param>
    /// <param name="exceptionPointer">异常执行指针</param>
    /// <param name="compensationStepId">补偿步骤ID</param>
    /// <returns>执行指针</returns>
    ExecutionPointer BuildCompensationPointer(WorkflowDefinition definition, ExecutionPointer parentPointer, ExecutionPointer exceptionPointer, int compensationStepId);
}
