using Atlas.Core.Setup;
using Atlas.WorkflowCore.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// WorkflowCore 工作流引擎托管服务
/// </summary>
public class WorkflowHostedService : IHostedService
{
    private readonly IWorkflowHost _workflowHost;
    private readonly ISetupStateProvider _setupStateProvider;
    private readonly ILogger<WorkflowHostedService> _logger;
    private readonly CancellationTokenSource _deferredStartCts = new();
    private Task? _deferredStartTask;

    public WorkflowHostedService(
        IWorkflowHost workflowHost,
        ISetupStateProvider setupStateProvider,
        ILogger<WorkflowHostedService> logger)
    {
        _workflowHost = workflowHost;
        _setupStateProvider = setupStateProvider;
        _logger = logger;
    }

    /// <summary>
    /// 启动工作流引擎
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_setupStateProvider.IsReady)
        {
            await _workflowHost.StartAsync(cancellationToken);
            return;
        }

        // setup 未完成时不阻塞宿主启动，改为后台等待就绪后再启动 WorkflowHost。
        _deferredStartTask = Task.Run(async () =>
        {
            try
            {
                await _setupStateProvider.WaitForReadyAsync(_deferredStartCts.Token);
                await _workflowHost.StartAsync(_deferredStartCts.Token);
                _logger.LogInformation("WorkflowHost started after setup became ready.");
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("WorkflowHost deferred start canceled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WorkflowHost deferred start failed.");
            }
        }, CancellationToken.None);
    }

    /// <summary>
    /// 停止工作流引擎
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _deferredStartCts.Cancel();
        if (_deferredStartTask is not null)
        {
            try
            {
                await _deferredStartTask;
            }
            catch (OperationCanceledException)
            {
                // expected during shutdown
            }
        }

        await _workflowHost.StopAsync(cancellationToken);
    }
}
