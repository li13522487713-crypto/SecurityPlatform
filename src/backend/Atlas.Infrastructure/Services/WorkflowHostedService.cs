using Atlas.WorkflowCore.Abstractions;
using Microsoft.Extensions.Hosting;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// WorkflowCore 工作流引擎托管服务
/// </summary>
public class WorkflowHostedService : IHostedService
{
    private readonly IWorkflowHost _workflowHost;

    public WorkflowHostedService(IWorkflowHost workflowHost)
    {
        _workflowHost = workflowHost;
    }

    /// <summary>
    /// 启动工作流引擎
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _workflowHost.StartAsync(cancellationToken);
    }

    /// <summary>
    /// 停止工作流引擎
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _workflowHost.StopAsync(cancellationToken);
    }
}
