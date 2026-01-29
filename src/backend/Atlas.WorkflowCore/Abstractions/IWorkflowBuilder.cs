using System;
using System.Collections.Generic;
using Atlas.WorkflowCore.Models;
using Atlas.WorkflowCore.Primitives;

namespace Atlas.WorkflowCore.Abstractions;

/// <summary>
/// 工作流构建器基接口
/// </summary>
public interface IWorkflowBuilder
{
    /// <summary>
    /// 步骤列表
    /// </summary>
    List<WorkflowStep> Steps { get; }

    /// <summary>
    /// 最后一个步骤ID
    /// </summary>
    int LastStep { get; }

    /// <summary>
    /// 使用指定的数据类型
    /// </summary>
    IWorkflowBuilder<T> UseData<T>();

    /// <summary>
    /// 构建工作流定义
    /// </summary>
    WorkflowDefinition Build(string id, int version);

    /// <summary>
    /// 添加步骤
    /// </summary>
    void AddStep(WorkflowStep step);

    /// <summary>
    /// 附加分支
    /// </summary>
    void AttachBranch(IWorkflowBuilder branch);
}

/// <summary>
/// 工作流构建器接口
/// </summary>
/// <typeparam name="TData">工作流数据类型</typeparam>
public interface IWorkflowBuilder<TData> : IWorkflowBuilder, IWorkflowModifier<TData, InlineStepBody>
{
    /// <summary>
    /// 使用步骤开始工作流
    /// </summary>
    IStepBuilder<TData, TStep> StartWith<TStep>(Action<IStepBuilder<TData, TStep>>? stepSetup = null) where TStep : IStepBody;

    /// <summary>
    /// 使用内联步骤开始工作流
    /// </summary>
    IStepBuilder<TData, InlineStepBody> StartWith(Func<IStepExecutionContext, ExecutionResult> body);

    /// <summary>
    /// 使用内联步骤开始工作流（带名称）
    /// </summary>
    IStepBuilder<TData, ActionStepBody> StartWith(Action<IStepExecutionContext> body);

    /// <summary>
    /// 获取指定步骤的上游步骤
    /// </summary>
    IEnumerable<WorkflowStep> GetUpstreamSteps(int id);

    /// <summary>
    /// 使用默认错误行为
    /// </summary>
    IWorkflowBuilder<TData> UseDefaultErrorBehavior(WorkflowErrorHandling behavior, TimeSpan? retryInterval = null);

    /// <summary>
    /// 创建分支
    /// </summary>
    IWorkflowBuilder<TData> CreateBranch();
}
