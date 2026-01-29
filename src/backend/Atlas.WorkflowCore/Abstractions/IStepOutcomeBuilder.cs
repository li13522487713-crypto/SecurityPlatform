using System;
using Atlas.WorkflowCore.Models;
using Atlas.WorkflowCore.Primitives;

namespace Atlas.WorkflowCore.Abstractions;

/// <summary>
/// 步骤结果构建器接口 - 用于When方法返回的构建器
/// </summary>
/// <typeparam name="TData">工作流数据类型</typeparam>
public interface IStepOutcomeBuilder<TData>
{
    /// <summary>
    /// 工作流构建器
    /// </summary>
    IWorkflowBuilder<TData> WorkflowBuilder { get; }

    /// <summary>
    /// 结果对象
    /// </summary>
    ValueOutcome Outcome { get; }

    /// <summary>
    /// 指定此结果的下一个步骤
    /// </summary>
    /// <typeparam name="TStep">步骤类型</typeparam>
    /// <param name="stepSetup">步骤配置</param>
    IStepBuilder<TData, TStep> Then<TStep>(Action<IStepBuilder<TData, TStep>>? stepSetup = null) where TStep : IStepBody;

    /// <summary>
    /// 指定此结果的下一个步骤
    /// </summary>
    /// <typeparam name="TStep">步骤类型</typeparam>
    /// <param name="step">步骤构建器</param>
    IStepBuilder<TData, TStep> Then<TStep>(IStepBuilder<TData, TStep> step) where TStep : IStepBody;

    /// <summary>
    /// 指定此结果的内联下一个步骤
    /// </summary>
    /// <param name="body">步骤体</param>
    IStepBuilder<TData, InlineStepBody> Then(Func<IStepExecutionContext, ExecutionResult> body);

    /// <summary>
    /// 结束工作流
    /// </summary>
    void EndWorkflow();
}
