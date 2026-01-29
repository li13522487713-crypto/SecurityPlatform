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
    /// <param name="def">工作流定义</param>
    /// <returns>执行指针</returns>
    ExecutionPointer BuildGenesisPointer(WorkflowDefinition def);

    /// <summary>
    /// 构建后续执行指针
    /// </summary>
    /// <param name="def">工作流定义</param>
    /// <param name="pointer">父执行指针</param>
    /// <param name="outcomeTarget">结果目标</param>
    /// <returns>执行指针</returns>
    ExecutionPointer BuildNextPointer(WorkflowDefinition def, ExecutionPointer pointer, IStepOutcome outcomeTarget);

    /// <summary>
    /// 构建子执行指针（用于容器步骤）
    /// </summary>
    /// <param name="def">工作流定义</param>
    /// <param name="pointer">父执行指针</param>
    /// <param name="childDefinitionId">子步骤定义ID</param>
    /// <param name="branch">分支对象</param>
    /// <returns>执行指针</returns>
    ExecutionPointer BuildChildPointer(WorkflowDefinition def, ExecutionPointer pointer, int childDefinitionId, object branch);

    /// <summary>
    /// 构建补偿执行指针
    /// </summary>
    /// <param name="def">工作流定义</param>
    /// <param name="pointer">父执行指针</param>
    /// <param name="exceptionPointer">异常执行指针</param>
    /// <param name="compensationStepId">补偿步骤ID</param>
    /// <returns>执行指针</returns>
    ExecutionPointer BuildCompensationPointer(WorkflowDefinition def, ExecutionPointer pointer, ExecutionPointer exceptionPointer, int compensationStepId);
}
