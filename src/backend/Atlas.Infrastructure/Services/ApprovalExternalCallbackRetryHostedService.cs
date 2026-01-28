using Atlas.Application.Approval.Repositories;
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

    public ApprovalExternalCallbackRetryHostedService(
        IServiceProvider serviceProvider,
        ILogger<ApprovalExternalCallbackRetryHostedService> logger,
        TimeProvider timeProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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

        // TODO: 如果需要多租户支持，需要遍历所有租户
        var tenantId = tenantProvider.GetTenantId();
        await callbackService.RetryFailedCallbacksAsync(tenantId, cancellationToken);
    }
}
