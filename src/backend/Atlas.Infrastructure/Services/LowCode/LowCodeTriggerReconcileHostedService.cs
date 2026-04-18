using Atlas.Application.LowCode.Repositories;
using Atlas.Core.Tenancy;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.LowCode;

/// <summary>
/// 启动时把数据库中所有 enabled cron 触发器同步注册到 Hangfire RecurringJob。
///
/// - 解决场景：服务进程重启后内存型 RecurringJob 仍由 Hangfire SQLite 持久化保留，但若新增了
///   触发器或 storage 被清理，仍需要从权威表 LowCodeTrigger 兜底恢复。
/// - 仅注册 kind=cron 且 Enabled 且 Cron 非空的触发器；其它跳过。
/// - 跨租户：扫描所有租户（按数据库行）。
/// - 失败：单条失败不影响其它，记录日志。
/// </summary>
public sealed class LowCodeTriggerReconcileHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LowCodeTriggerReconcileHostedService> _logger;

    public LowCodeTriggerReconcileHostedService(IServiceScopeFactory scopeFactory, ILogger<LowCodeTriggerReconcileHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ILowCodeTriggerRepository>();
            var recurring = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
            var triggers = await repo.ListAllAsync(cancellationToken);
            var enabledCron = triggers
                .Where(t => string.Equals(t.Kind, "cron", StringComparison.OrdinalIgnoreCase) && t.Enabled && !string.IsNullOrWhiteSpace(t.Cron))
                .ToList();
            foreach (var t in enabledCron)
            {
                var jobId = $"lowcode-trigger:{t.TenantIdValue}:{t.TriggerId}";
                try
                {
                    var tenantValue = t.TenantIdValue;
                    var triggerId = t.TriggerId;
                    recurring.AddOrUpdate<LowCodeTriggerCronJob>(jobId, job => job.RunAsync(tenantValue, triggerId), t.Cron!);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Reconcile trigger {Trigger} failed", t.TriggerId);
                }
            }
            _logger.LogInformation("LowCodeTriggerReconcile: registered {Count} cron triggers", enabledCron.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LowCodeTriggerReconcile startup failed; cron triggers may be inactive until next upsert");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
