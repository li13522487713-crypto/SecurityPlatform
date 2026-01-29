using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Primitives;

/// <summary>
/// Saga 容器步骤体 - 支持补偿的事务性步骤容器
/// </summary>
public class SagaContainer : ContainerStepBody
{
    public override ExecutionResult Run(IStepExecutionContext context)
    {
        // Saga 容器由引擎处理子步骤和补偿逻辑
        return ExecutionResult.Next();
    }
}

/// <summary>
/// Saga 容器步骤定义 - 重写补偿相关属性
/// </summary>
public class SagaContainerStep : WorkflowStep<SagaContainer>
{
    /// <summary>
    /// 补偿后不继续执行子步骤
    /// </summary>
    public override bool ResumeChildrenAfterCompensation => false;

    /// <summary>
    /// 补偿时回滚同级步骤
    /// </summary>
    public override bool RevertChildrenAfterCompensation => true;

    /// <summary>
    /// 重试时清空持久化数据
    /// </summary>
    public override void PrimeForRetry(ExecutionPointer pointer)
    {
        base.PrimeForRetry(pointer);
        pointer.PersistenceData = null;
    }
}
