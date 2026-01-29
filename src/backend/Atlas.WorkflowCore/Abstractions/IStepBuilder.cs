using System.Collections;
using System.Linq.Expressions;
using Atlas.WorkflowCore.Models;
using Atlas.WorkflowCore.Primitives;

namespace Atlas.WorkflowCore.Abstractions;

public interface IStepBuilder<TData>
{
    // 步骤配置
    IStepBuilder<TData> Name(string name);
    IStepBuilder<TData> Id(string id);

    // 输入输出映射
    IStepBuilder<TData> Input<TInput>(Expression<Func<TData, TInput>> value);
    IStepBuilder<TData> Input<TInput>(string name, TInput value);
    IStepBuilder<TData> Input<TInput>(string name, Func<TData, TInput> value);

    IStepBuilder<TData> Output<TInput>(Expression<Func<TData, TInput>> value, Expression<Func<IStepExecutionContext, TInput>> assign);
    IStepBuilder<TData> Output<TInput>(string name, Expression<Func<IStepExecutionContext, TInput>> assign);

    // 流程控制
    IStepBuilder<TData> Then<TStep>(Action<IStepBuilder<TData>>? stepSetup = null)
        where TStep : IStepBody;
    IStepBuilder<TData> Then(Func<IStepExecutionContext, ExecutionResult> body);
    IStepBuilder<TData> Then(string name, Func<IStepExecutionContext, ExecutionResult> body);

    // 错误处理和补偿
    IStepBuilder<TData> OnError(WorkflowErrorHandling behavior, TimeSpan? retryInterval = null);
    IStepBuilder<TData> CompensateWith<TStep>(Action<IStepBuilder<TData>>? stepSetup = null) where TStep : IStepBody;
    IStepBuilder<TData> CompensateWith(Func<IStepExecutionContext, ExecutionResult> body);
    IStepBuilder<TData> CompensateWithSequence(Action<IWorkflowBuilder<TData>> builder);

    // 取消条件
    IStepBuilder<TData> CancelCondition(Expression<Func<TData, bool>> cancelCondition, bool proceedAfterCancel = false);

    // 控制流方法
    IStepBuilder<TData> WaitFor(string eventName, Expression<Func<TData, string>> eventKey, 
        Expression<Func<TData, DateTime>>? effectiveDate = null);
    
    IStepBuilder<TData> Delay(Expression<Func<TData, TimeSpan>> period);
    
    IContainerStepBuilder<TData, IStepBuilder<TData>> Decide(Expression<Func<TData, object>> expression);
    
    IContainerStepBuilder<TData, IStepBuilder<TData>> ForEach(Expression<Func<TData, IEnumerable>> collection);
    
    IContainerStepBuilder<TData, IStepBuilder<TData>> While(Expression<Func<TData, bool>> condition);
    
    IContainerStepBuilder<TData, IStepBuilder<TData>> If(Expression<Func<TData, bool>> condition);
    
    IStepBuilder<TData> When(object outcomeValue, string? label = null);
    
    IContainerStepBuilder<TData, IStepBuilder<TData>> Parallel();
    
    IContainerStepBuilder<TData, IStepBuilder<TData>> Saga();
    
    IContainerStepBuilder<TData, IStepBuilder<TData>> Schedule(Expression<Func<TData, TimeSpan>> time);
    
    IContainerStepBuilder<TData, IStepBuilder<TData>> Recur(Expression<Func<TData, TimeSpan>> interval, 
        Expression<Func<TData, bool>>? until = null);
    
    IStepBuilder<TData> Activity(string activityName, Expression<Func<TData, object>>? parameters = null,
        Expression<Func<TData, DateTime>>? effectiveDate = null);

    IWorkflowBuilder<TData> End(string? name = null);
}
