using Atlas.Application.DynamicTables.Abstractions;
using Atlas.Core.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 后台汇总计算工作者：通过 Channel 队列异步处理 Lookup 关系的汇总任务，
/// 避免阻塞请求链路并支持削峰。
/// </summary>
public sealed class RollupBackgroundWorker : BackgroundService
{
    private readonly Channel<RollupTask> _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RollupBackgroundWorker> _logger;

    public RollupBackgroundWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<RollupBackgroundWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _queue = Channel.CreateBounded<RollupTask>(new BoundedChannelOptions(500)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        });
    }

    /// <summary>
    /// 将汇总任务入队（由 Lookup 写入路径调用，非阻塞）。
    /// </summary>
    public bool Enqueue(TenantId tenantId, string masterTableKey, long masterRecordId)
    {
        return _queue.Writer.TryWrite(new RollupTask(tenantId, masterTableKey, masterRecordId));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[RollupWorker] 后台汇总计算工作者已启动。");

        await foreach (var task in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var rollupService = scope.ServiceProvider.GetRequiredService<IRollupCalculationService>();
                await rollupService.RecalculateAsync(task.TenantId, task.MasterTableKey, task.MasterRecordId, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[RollupWorker] 汇总计算异常。Table={Table}, RecordId={Id}",
                    task.MasterTableKey, task.MasterRecordId);
            }
        }

        _logger.LogInformation("[RollupWorker] 后台汇总计算工作者已停止。");
    }
}

/// <summary>汇总任务条目</summary>
public sealed record RollupTask(TenantId TenantId, string MasterTableKey, long MasterRecordId);
