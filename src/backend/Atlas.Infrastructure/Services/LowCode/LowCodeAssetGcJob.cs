using Atlas.Application.LowCode.Abstractions;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.LowCode;

/// <summary>
/// 低代码资产 GC 作业（M10 S10-3）。
/// 每天凌晨 2 点扫描过期 pending 会话；7 天内未引用 fileHandle 的资产由 IFileStorageService 软删除（M10 复用既有 GC 链）。
/// </summary>
public sealed class LowCodeAssetGcJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LowCodeAssetGcJob> _logger;

    public LowCodeAssetGcJob(IServiceScopeFactory scopeFactory, ILogger<LowCodeAssetGcJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    [AutomaticRetry(Attempts = 2)]
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IRuntimeFileService>();
        var expired = await svc.RunGarbageCollectionAsync(cancellationToken);
        _logger.LogInformation("LowCodeAssetGcJob: expired pending sessions = {Count}", expired);
    }
}

/// <summary>注册 Hangfire 周期任务（每日 02:00）。AppHost 启动时调用。</summary>
public sealed class LowCodeAssetGcSchedulerHostedService : IHostedService
{
    private readonly IRecurringJobManager _recurring;

    public LowCodeAssetGcSchedulerHostedService(IRecurringJobManager recurring)
    {
        _recurring = recurring;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _recurring.AddOrUpdate<LowCodeAssetGcJob>(
            recurringJobId: "lowcode-asset-gc",
            methodCall: job => job.RunAsync(CancellationToken.None),
            cronExpression: Cron.Daily(2, 0));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
