using Atlas.Application.System.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 定时检查到期租户并自动停用。
/// 每天检查一次，等保2.0 租户管控要求。
/// </summary>
public sealed class TenantExpirationHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TenantExpirationHostedService> _logger;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

    public TenantExpirationHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<TenantExpirationHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TenantExpirationHostedService started, check interval: {Hours}h", CheckInterval.TotalHours);

        // 启动后延迟 5 分钟再执行，避免争抢 DB 初始化
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var tenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();
                var suspended = await tenantService.CheckAndSuspendExpiredTenantsAsync(stoppingToken);
                if (suspended > 0)
                {
                    _logger.LogWarning("TenantExpirationCheck: 自动停用了 {Count} 个已到期租户", suspended);
                }
                else
                {
                    _logger.LogDebug("TenantExpirationCheck: 无到期租户需要处理");
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TenantExpirationCheck: 到期检查失败");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }
}
