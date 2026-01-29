using System;
using Atlas.WorkflowCore.Primitives;

namespace Atlas.WorkflowCore.Abstractions;

/// <summary>
/// 并行步骤构建器接口
/// </summary>
/// <typeparam name="TData">工作流数据类型</typeparam>
/// <typeparam name="TContainer">容器类型</typeparam>
public interface IParallelStepBuilder<TData, TContainer>
    where TContainer : IStepBody
{
    /// <summary>
    /// 定义并行分支
    /// </summary>
    IStepBuilder<TData, TContainer> Do(Action<IStepBuilder<TData, InlineStepBody>>? branch = null);

    /// <summary>
    /// 结束并行块并返回序列步骤
    /// </summary>
    IStepBuilder<TData, Sequence> Join();
}
