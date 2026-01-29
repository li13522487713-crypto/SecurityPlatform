using System;
using System.Collections;
using System.Linq.Expressions;
using Atlas.WorkflowCore.Models;
using Atlas.WorkflowCore.Primitives;

namespace Atlas.WorkflowCore.Abstractions;

/// <summary>
/// 工作流修改器接口 - 包含所有控制流方法
/// </summary>
/// <typeparam name="TData">工作流数据类型</typeparam>
/// <typeparam name="TStepBody">当前步骤体类型</typeparam>
public interface IWorkflowModifier<TData, TStepBody>
    where TStepBody : IStepBody
{
    /// <summary>
    /// 指定工作流中的下一个步骤
    /// </summary>
    /// <typeparam name="TStep">要执行的步骤类型</typeparam>
    /// <param name="stepSetup">配置此步骤的额外参数</param>
    IStepBuilder<TData, TStep> Then<TStep>(Action<IStepBuilder<TData, TStep>>? stepSetup = null) where TStep : IStepBody;

    /// <summary>
    /// 指定工作流中的下一个步骤
    /// </summary>
    /// <typeparam name="TStep">步骤类型</typeparam>
    /// <param name="newStep">新步骤构建器</param>
    IStepBuilder<TData, TStep> Then<TStep>(IStepBuilder<TData, TStep> newStep) where TStep : IStepBody;

    /// <summary>
    /// 指定工作流中的内联下一个步骤
    /// </summary>
    /// <param name="body">步骤体</param>
    IStepBuilder<TData, InlineStepBody> Then(Func<IStepExecutionContext, ExecutionResult> body);

    /// <summary>
    /// 指定工作流中的内联下一个步骤
    /// </summary>
    /// <param name="body">步骤体</param>
    IStepBuilder<TData, ActionStepBody> Then(Action<IStepExecutionContext> body);

    /// <summary>
    /// 等待指定事件发布
    /// </summary>
    /// <param name="eventName">用于标识要等待的事件类型的名称</param>
    /// <param name="eventKey">事件上下文中要等待的特定键值</param>
    /// <param name="effectiveDate">从此有效日期开始监听事件</param>
    /// <param name="cancelCondition">为true时将取消此WaitFor的条件</param>
    IStepBuilder<TData, WaitFor> WaitFor(string eventName, Expression<Func<TData, string>> eventKey,
        Expression<Func<TData, DateTime>>? effectiveDate = null, Expression<Func<TData, bool>>? cancelCondition = null);

    /// <summary>
    /// 等待指定事件发布（带执行上下文）
    /// </summary>
    /// <param name="eventName">用于标识要等待的事件类型的名称</param>
    /// <param name="eventKey">事件上下文中要等待的特定键值</param>
    /// <param name="effectiveDate">从此有效日期开始监听事件</param>
    /// <param name="cancelCondition">为true时将取消此WaitFor的条件</param>
    IStepBuilder<TData, WaitFor> WaitFor(string eventName,
        Expression<Func<TData, IStepExecutionContext, string>> eventKey,
        Expression<Func<TData, DateTime>>? effectiveDate = null, Expression<Func<TData, bool>>? cancelCondition = null);

    /// <summary>
    /// 等待指定时间段
    /// </summary>
    /// <param name="period">等待时间段</param>
    IStepBuilder<TData, Delay> Delay(Expression<Func<TData, TimeSpan>> period);

    /// <summary>
    /// 评估表达式并根据值采取不同的路径
    /// </summary>
    /// <param name="expression">用于决策的表达式</param>
    IStepBuilder<TData, Decide> Decide(Expression<Func<TData, object>> expression);

    /// <summary>
    /// 对集合中的每个项执行一个步骤块（并行foreach）
    /// </summary>
    /// <param name="collection">要迭代的集合</param>
    IContainerStepBuilder<TData, Foreach, Foreach> ForEach(Expression<Func<TData, IEnumerable>> collection);

    /// <summary>
    /// 对集合中的每个项执行一个步骤块（可配置并行）
    /// </summary>
    /// <param name="collection">要迭代的集合</param>
    /// <param name="runParallel">是否并行运行</param>
    IContainerStepBuilder<TData, Foreach, Foreach> ForEach(Expression<Func<TData, IEnumerable>> collection, Expression<Func<TData, bool>> runParallel);

    /// <summary>
    /// 对集合中的每个项执行一个步骤块（带执行上下文）
    /// </summary>
    /// <param name="collection">要迭代的集合</param>
    /// <param name="runParallel">是否并行运行</param>
    IContainerStepBuilder<TData, Foreach, Foreach> ForEach(Expression<Func<TData, IStepExecutionContext, IEnumerable>> collection, Expression<Func<TData, bool>> runParallel);

    /// <summary>
    /// 重复步骤块直到条件为true
    /// </summary>
    /// <param name="condition">用于跳出while循环的条件</param>
    IContainerStepBuilder<TData, While, While> While(Expression<Func<TData, bool>> condition);

    /// <summary>
    /// 重复步骤块直到条件为true（带执行上下文）
    /// </summary>
    /// <param name="condition">用于跳出while循环的条件</param>
    IContainerStepBuilder<TData, While, While> While(Expression<Func<TData, IStepExecutionContext, bool>> condition);

    /// <summary>
    /// 如果条件为true则执行步骤块
    /// </summary>
    /// <param name="condition">要评估的条件</param>
    IContainerStepBuilder<TData, If, If> If(Expression<Func<TData, bool>> condition);

    /// <summary>
    /// 如果条件为true则执行步骤块（带执行上下文）
    /// </summary>
    /// <param name="condition">要评估的条件</param>
    IContainerStepBuilder<TData, If, If> If(Expression<Func<TData, IStepExecutionContext, bool>> condition);

    /// <summary>
    /// 为此步骤配置结果，然后将其连接到序列
    /// </summary>
    /// <param name="outcomeValue">结果值</param>
    /// <param name="label">结果标签</param>
    IContainerStepBuilder<TData, When, OutcomeSwitch> When(Expression<Func<TData, object>> outcomeValue, string? label = null);

    /// <summary>
    /// 并行执行多个步骤块
    /// </summary>
    IParallelStepBuilder<TData, Sequence> Parallel();

    /// <summary>
    /// 在容器中执行步骤序列（Saga事务）
    /// </summary>
    IStepBuilder<TData, Sequence> Saga(Action<IWorkflowBuilder<TData>> builder);

    /// <summary>
    /// 计划在未来某个时间并行执行步骤块
    /// </summary>
    /// <param name="time">执行块之前等待的时间跨度</param>
    IContainerStepBuilder<TData, Schedule, TStepBody> Schedule(Expression<Func<TData, TimeSpan>> time);

    /// <summary>
    /// 计划在未来某个时间以重复间隔并行执行步骤块
    /// </summary>
    /// <param name="interval">重复执行之间等待的时间跨度</param>
    /// <param name="until">用于停止重复任务的条件</param>
    IContainerStepBuilder<TData, Recur, TStepBody> Recur(Expression<Func<TData, TimeSpan>> interval,
        Expression<Func<TData, bool>> until);

    /// <summary>
    /// 等待外部活动完成
    /// </summary>
    /// <param name="activityName">用于标识要等待的活动的名称</param>
    /// <param name="parameters">传递给外部活动工作者的数据</param>
    /// <param name="effectiveDate">从此有效日期开始监听事件</param>
    /// <param name="cancelCondition">为true时将取消此活动的条件</param>
    IStepBuilder<TData, Activity> Activity(string activityName, Expression<Func<TData, object>>? parameters = null,
        Expression<Func<TData, DateTime>>? effectiveDate = null, Expression<Func<TData, bool>>? cancelCondition = null);

    /// <summary>
    /// 等待外部活动完成（带执行上下文）
    /// </summary>
    /// <param name="activityName">用于标识要等待的活动的名称</param>
    /// <param name="parameters">传递给外部活动工作者的数据</param>
    /// <param name="effectiveDate">从此有效日期开始监听事件</param>
    /// <param name="cancelCondition">为true时将取消此活动的条件</param>
    IStepBuilder<TData, Activity> Activity(Expression<Func<TData, IStepExecutionContext, string>> activityName, Expression<Func<TData, object>>? parameters = null,
        Expression<Func<TData, DateTime>>? effectiveDate = null, Expression<Func<TData, bool>>? cancelCondition = null);
}
