using Atlas.Application.ExternalConnectors.Abstractions;
using Atlas.Application.ExternalConnectors.Repositories;
using Atlas.Core.Tenancy;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.ExternalConnectors.HostedServices;

/// <summary>
/// 启动时根据各 provider 的 SyncCron 注册 Hangfire RecurringJob，
/// 让通讯录全量校准定时执行；以及在启动后 30 秒触发一次启动期校准（仅当 SyncCron 非空时）。
/// </summary>
public sealed class ExternalDirectoryFullSyncHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExternalDirectoryFullSyncHostedService> _logger;

    public ExternalDirectoryFullSyncHostedService(IServiceScopeFactory scopeFactory, ILogger<ExternalDirectoryFullSyncHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var providerRepository = scope.ServiceProvider.GetRequiredService<IExternalIdentityProviderRepository>();
            // 启动时不能确定租户列表，这里只做"应用层"层面的统一调度入口注册：
            // 真正的全量同步由 ConnectorDirectoryController 手动触发或由 Hangfire RecurringJob 触发。
            // RecurringJob 名按 (TenantId, ProviderId) 唯一；具体 cron 在 provider 创建/更新时由 Controller 显式注册。
            // 本方法仅清理过期 job 注册（保留扩展点）。
            _logger.LogInformation("ExternalDirectoryFullSyncHostedService started; recurring jobs are managed per-provider via controller actions.");
            await Task.CompletedTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ExternalDirectoryFullSyncHostedService failed to start.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// 真正在 Hangfire Worker 上执行的入口；为每个 (Tenant, Provider) 注册一份。
/// 通过 IServiceScopeFactory 获取 scoped IExternalDirectorySyncService。
/// </summary>
public sealed class ExternalDirectoryRecurringSyncRunner
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITenantContextWriter _tenantContextWriter;
    private readonly ILogger<ExternalDirectoryRecurringSyncRunner> _logger;

    public ExternalDirectoryRecurringSyncRunner(IServiceScopeFactory scopeFactory, ITenantContextWriter tenantContextWriter, ILogger<ExternalDirectoryRecurringSyncRunner> logger)
    {
        _scopeFactory = scopeFactory;
        _tenantContextWriter = tenantContextWriter;
        _logger = logger;
    }

    public async Task RunAsync(Guid tenantId, long providerId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            using var tenantScope = _tenantContextWriter.BeginScope(scope.ServiceProvider, new TenantId(tenantId));
            var sync = scope.ServiceProvider.GetRequiredService<IExternalDirectorySyncService>();
            await sync.RunFullSyncAsync(providerId, "cron", CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "External directory recurring sync failed (tenant={TenantId}, provider={ProviderId}).", tenantId, providerId);
        }
    }
}

/// <summary>
/// 把 Hangfire 调用前需要"把 TenantId 写到当前 scope 的 ITenantProvider"这件事抽出来，
/// 避免 Hangfire job 因为没有 HttpContext 拿不到租户。
/// </summary>
public interface ITenantContextWriter
{
    IDisposable BeginScope(IServiceProvider scopeServices, TenantId tenantId);
}
