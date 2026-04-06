using Atlas.Core.Setup;
using Atlas.Infrastructure.Services.ApprovalFlow.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 定时器节点后台服务（定时扫描到期的 Timer/Trigger 节点并推进流程）
/// 扫描间隔：60 秒
/// </summary>
public sealed class ApprovalTimerNodeHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ApprovalTimerNodeHostedService> _logger;
    private readonly TimeSpan _scanInterval = TimeSpan.FromSeconds(60);
    private readonly ISetupStateProvider _setupStateProvider;

    public ApprovalTimerNodeHostedService(
        IServiceProvider serviceProvider,
        ILogger<ApprovalTimerNodeHostedService> logger,
        ISetupStateProvider setupStateProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _setupStateProvider = setupStateProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _setupStateProvider.WaitForReadyAsync(stoppingToken);
        _logger.LogInformation("审批定时器节点后台服务已启动");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunJobsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "扫描审批定时器/触发器节点时发生错误");
            }

            await Task.Delay(_scanInterval, stoppingToken);
        }

        _logger.LogInformation("审批定时器节点后台服务已停止");
    }

    private async Task RunJobsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var sp = scope.ServiceProvider;

        var timerJob = sp.GetRequiredService<ApprovalTimerNodeJob>();
        await timerJob.ExecuteAsync(cancellationToken);

        var triggerJob = sp.GetRequiredService<ApprovalTriggerNodeJob>();
        await triggerJob.ExecuteAsync(cancellationToken);
    }
}
