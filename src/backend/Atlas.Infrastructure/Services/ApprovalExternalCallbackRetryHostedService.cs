using Atlas.Application.Approval.Repositories;
using Atlas.Core.Setup;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Services.ApprovalFlow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 外部回调重试后台服务（定时扫描并重试失败的回调）
/// </summary>
public sealed class ApprovalExternalCallbackRetryHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ApprovalExternalCallbackRetryHostedService> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _scanInterval = TimeSpan.FromMinutes(5); // 每5分钟扫描一次
    private readonly ISetupStateProvider _setupStateProvider;

    public ApprovalExternalCallbackRetryHostedService(
        IServiceProvider serviceProvider,
        ILogger<ApprovalExternalCallbackRetryHostedService> logger,
        TimeProvider timeProvider,
        ISetupStateProvider setupStateProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _timeProvider = timeProvider;
        _setupStateProvider = setupStateProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _setupStateProvider.WaitForReadyAsync(stoppingToken);
        _logger.LogInformation("外部回调重试服务已启动");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RetryFailedCallbacksAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "重试失败回调时发生错误");
            }

            await Task.Delay(_scanInterval, stoppingToken);
        }

        _logger.LogInformation("外部回调重试服务已停止");
    }

    private async Task RetryFailedCallbacksAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var callbackService = scope.ServiceProvider.GetService<ExternalCallbackService>();
        var tenantProvider = scope.ServiceProvider.GetRequiredService<ITenantProvider>();

        if (callbackService == null)
        {
            return; // 如果没有配置回调服务，跳过
        }

        // 当前约束：重试任务仅处理当前租户数据；跨租户重试将由平台级调度统一执行。
        // 跟踪任务：APRV-CB-41（https://tracker.local/APRV-CB-41），预计版本：v1.6。
        var tenantId = tenantProvider.GetTenantId();
        await callbackService.RetryFailedCallbacksAsync(tenantId, cancellationToken);
    }
}
