using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Services;

/// <summary>
/// 执行工作流步骤并应用任何已注册的 IWorkflowStepMiddleware
/// </summary>
public class StepExecutor : IStepExecutor
{
    private readonly IEnumerable<IWorkflowStepMiddleware> _stepMiddleware;

    public StepExecutor(IEnumerable<IWorkflowStepMiddleware> stepMiddleware)
    {
        _stepMiddleware = stepMiddleware;
    }

    /// <summary>
    /// 在给定的 IStepExecutionContext 中运行传递的 IStepBody，同时应用系统中注册的任何 IWorkflowStepMiddleware。
    /// 中间件将按照在 DI 中注册的顺序运行，先声明的中间件先开始，后完成。
    /// </summary>
    /// <param name="context">执行步骤的上下文</param>
    /// <param name="body">步骤体</param>
    /// <returns>运行步骤的结果</returns>
    public async Task<ExecutionResult> ExecuteStep(IStepExecutionContext context, IStepBody body)
    {
        // 通过反向聚合所有中间件来构建中间件链，从步骤体开始
        // 构建调用链中下一个委托的步骤委托
        Task<ExecutionResult> Step() => body.RunAsync(context);
        
        var middlewareChain = _stepMiddleware
            .Reverse()
            .Aggregate(
                (WorkflowStepDelegate)Step,
                (previous, middleware) => () => middleware.HandleAsync(context, body, previous)
            );

        // 运行中间件链
        return await middlewareChain();
    }
}
