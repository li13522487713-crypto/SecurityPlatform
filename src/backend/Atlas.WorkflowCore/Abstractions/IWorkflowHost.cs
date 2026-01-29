namespace Atlas.WorkflowCore.Abstractions;

/// <summary>
/// 工作流主机接口 - 管理工作流引擎的生命周期
/// </summary>
public interface IWorkflowHost : IWorkflowController
{
    /// <summary>
    /// 启动工作流主机
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止工作流主机
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 注册工作流定义
    /// </summary>
    /// <typeparam name="TWorkflow">工作流类型</typeparam>
    void RegisterWorkflow<TWorkflow>() where TWorkflow : IWorkflow;

    /// <summary>
    /// 注册工作流定义（带数据类型）
    /// </summary>
    /// <typeparam name="TWorkflow">工作流类型</typeparam>
    /// <typeparam name="TData">数据类型</typeparam>
    void RegisterWorkflow<TWorkflow, TData>() where TWorkflow : IWorkflow<TData> where TData : class, new();

    /// <summary>
    /// 报告步骤错误
    /// </summary>
    /// <param name="workflow">工作流实例</param>
    /// <param name="step">步骤定义</param>
    /// <param name="exception">异常</param>
    void ReportStepError(Models.WorkflowInstance workflow, Models.WorkflowStep step, Exception exception);
}
