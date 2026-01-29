using System;
using System.Collections;
using System.Linq.Expressions;
using Atlas.WorkflowCore.Models;
using Atlas.WorkflowCore.Primitives;

namespace Atlas.WorkflowCore.Abstractions;

/// <summary>
/// 步骤构建器接口
/// </summary>
/// <typeparam name="TData">工作流数据类型</typeparam>
/// <typeparam name="TStepBody">步骤体类型</typeparam>
public interface IStepBuilder<TData, TStepBody> : IWorkflowModifier<TData, TStepBody>
    where TStepBody : IStepBody
{
    /// <summary>
    /// 工作流构建器
    /// </summary>
    IWorkflowBuilder<TData> WorkflowBuilder { get; }

    /// <summary>
    /// 步骤对象
    /// </summary>
    WorkflowStep<TStepBody> Step { get; set; }

    /// <summary>
    /// 指定步骤的显示名称
    /// </summary>
    /// <param name="name">步骤的显示名称</param>
    IStepBuilder<TData, TStepBody> Name(string name);

    /// <summary>
    /// 指定自定义ID以引用此步骤
    /// </summary>
    /// <param name="id">自定义ID</param>
    IStepBuilder<TData, TStepBody> Id(string id);

    /// <summary>
    /// 通过ID指定工作流中的下一个步骤
    /// </summary>
    /// <param name="id">步骤ID</param>
    IStepBuilder<TData, TStepBody> Attach(string id);

    /// <summary>
    /// 配置此步骤的结果分支（用于简单的值分支）
    /// </summary>
    /// <param name="outcomeValue">结果值</param>
    /// <param name="label">结果标签</param>
    IStepOutcomeBuilder<TData> When(object outcomeValue, string? label = null);

    /// <summary>
    /// 配置此步骤的结果分支，然后将其连接到另一个步骤
    /// </summary>
    /// <param name="outcomeValue">结果值</param>
    /// <param name="branch">分支构建器</param>
    IStepBuilder<TData, TStepBody> Branch<TStep>(object outcomeValue, IStepBuilder<TData, TStep> branch) where TStep : IStepBody;

    /// <summary>
    /// 配置此步骤的结果分支，然后将其连接到另一个步骤
    /// </summary>
    /// <param name="outcomeExpression">结果表达式</param>
    /// <param name="branch">分支构建器</param>
    IStepBuilder<TData, TStepBody> Branch<TStep>(Expression<Func<TData, object, bool>> outcomeExpression, IStepBuilder<TData, TStep> branch) where TStep : IStepBody;

    /// <summary>
    /// 在步骤执行前将步骤的属性映射到工作流数据对象的属性
    /// </summary>
    /// <typeparam name="TInput">输入类型</typeparam>
    /// <param name="stepProperty">步骤上的属性</param>
    /// <param name="value">数据对象上的属性</param>
    IStepBuilder<TData, TStepBody> Input<TInput>(Expression<Func<TStepBody, TInput>> stepProperty, Expression<Func<TData, TInput>> value);

    /// <summary>
    /// 在步骤执行前将步骤的属性映射到工作流数据对象的属性（带执行上下文）
    /// </summary>
    /// <typeparam name="TInput">输入类型</typeparam>
    /// <param name="stepProperty">步骤上的属性</param>
    /// <param name="value">数据对象上的属性（带上下文）</param>
    IStepBuilder<TData, TStepBody> Input<TInput>(Expression<Func<TStepBody, TInput>> stepProperty, Expression<Func<TData, IStepExecutionContext, TInput>> value);

    /// <summary>
    /// 在步骤执行前操作步骤的属性
    /// </summary>
    /// <param name="action">操作委托</param>
    IStepBuilder<TData, TStepBody> Input(Action<TStepBody, TData> action);

    /// <summary>
    /// 在步骤执行前操作步骤的属性（带执行上下文）
    /// </summary>
    /// <param name="action">操作委托（带上下文）</param>
    IStepBuilder<TData, TStepBody> Input(Action<TStepBody, TData, IStepExecutionContext> action);

    /// <summary>
    /// 在步骤执行后将工作流数据对象的属性映射到步骤的属性
    /// </summary>
    /// <typeparam name="TOutput">输出类型</typeparam>
    /// <param name="dataProperty">数据对象上的属性</param>
    /// <param name="value">步骤上的属性</param>
    IStepBuilder<TData, TStepBody> Output<TOutput>(Expression<Func<TData, TOutput>> dataProperty, Expression<Func<TStepBody, object>> value);

    /// <summary>
    /// 在步骤执行后操作数据对象的属性
    /// </summary>
    /// <param name="action">操作委托</param>
    IStepBuilder<TData, TStepBody> Output(Action<TStepBody, TData> action);

    /// <summary>
    /// 返回到指定名称的父步骤
    /// </summary>
    /// <typeparam name="TStep">父步骤类型</typeparam>
    /// <param name="name">父步骤名称</param>
    IStepBuilder<TData, TStep> End<TStep>(string name) where TStep : IStepBody;

    /// <summary>
    /// 配置此步骤抛出未处理异常时的行为
    /// </summary>
    /// <param name="behavior">步骤抛出未处理异常时采取的操作</param>
    /// <param name="retryInterval">如果行为是重试，重试间隔</param>
    IStepBuilder<TData, TStepBody> OnError(WorkflowErrorHandling behavior, TimeSpan? retryInterval = null);

    /// <summary>
    /// 结束工作流并标记为完成
    /// </summary>
    IStepBuilder<TData, TStepBody> EndWorkflow();

    /// <summary>
    /// 如果此步骤抛出未处理异常，则撤销步骤
    /// </summary>
    /// <typeparam name="TStep">要执行的步骤类型</typeparam>
    /// <param name="stepSetup">配置此步骤的额外参数</param>
    IStepBuilder<TData, TStepBody> CompensateWith<TStep>(Action<IStepBuilder<TData, TStep>>? stepSetup = null) where TStep : IStepBody;

    /// <summary>
    /// 如果此步骤抛出未处理异常，则撤销步骤
    /// </summary>
    /// <param name="body">补偿步骤体</param>
    IStepBuilder<TData, TStepBody> CompensateWith(Func<IStepExecutionContext, ExecutionResult> body);

    /// <summary>
    /// 如果此步骤抛出未处理异常，则撤销步骤
    /// </summary>
    /// <param name="body">补偿步骤体</param>
    IStepBuilder<TData, TStepBody> CompensateWith(Action<IStepExecutionContext> body);

    /// <summary>
    /// 如果此步骤抛出未处理异常，则撤销步骤（序列补偿）
    /// </summary>
    /// <param name="builder">补偿序列构建器</param>
    IStepBuilder<TData, TStepBody> CompensateWithSequence(Action<IWorkflowBuilder<TData>> builder);

    /// <summary>
    /// 在满足条件时提前取消此步骤的执行
    /// </summary>
    /// <param name="cancelCondition">取消条件</param>
    /// <param name="proceedAfterCancel">取消后是否继续</param>
    IStepBuilder<TData, TStepBody> CancelCondition(Expression<Func<TData, bool>> cancelCondition, bool proceedAfterCancel = false);
}
