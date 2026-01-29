using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Abstractions;

/// <summary>
/// 执行指针工厂接口
/// </summary>
public interface IExecutionPointerFactory
{
    /// <summary>
    /// 创建初始执行指针
    /// </summary>
    /// <param name="step">步骤定义</param>
    /// <returns>执行指针</returns>
    ExecutionPointer CreateInitialPointer(WorkflowStep step);

    /// <summary>
    /// 创建后续执行指针
    /// </summary>
    /// <param name="step">步骤定义</param>
    /// <param name="parentPointer">父执行指针</param>
    /// <returns>执行指针</returns>
    ExecutionPointer CreateNextPointer(WorkflowStep step, ExecutionPointer parentPointer);

    /// <summary>
    /// 创建子执行指针（用于容器步骤）
    /// </summary>
    /// <param name="step">步骤定义</param>
    /// <param name="parentPointer">父执行指针</param>
    /// <param name="scope">作用域</param>
    /// <returns>执行指针</returns>
    ExecutionPointer CreateChildPointer(WorkflowStep step, ExecutionPointer parentPointer, string scope);

    /// <summary>
    /// 创建补偿执行指针
    /// </summary>
    /// <param name="step">步骤定义</param>
    /// <param name="parentPointer">父执行指针</param>
    /// <returns>执行指针</returns>
    ExecutionPointer CreateCompensationPointer(WorkflowStep step, ExecutionPointer parentPointer);
}
