using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Abstractions;

/// <summary>
/// 工作流步骤中间件接口
/// </summary>
public interface IWorkflowStepMiddleware
{
    /// <summary>
    /// 处理工作流步骤并返回执行结果
    /// 重要：必须在中间件中调用 next 委托，否则步骤将不会被执行
    /// </summary>
    /// <param name="context">步骤上下文</param>
    /// <param name="body">要执行的步骤体实例</param>
    /// <param name="next">链中的下一个中间件</param>
    /// <returns>工作流执行结果</returns>
    Task<ExecutionResult> HandleAsync(
        IStepExecutionContext context,
        IStepBody body,
        WorkflowStepDelegate next);
}

/// <summary>
/// 工作流步骤委托类型
/// </summary>
public delegate Task<ExecutionResult> WorkflowStepDelegate();
