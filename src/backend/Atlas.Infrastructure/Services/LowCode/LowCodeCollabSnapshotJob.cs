using Atlas.Application.Audit.Abstractions;
using Atlas.Application.LowCode.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Audit.Entities;
using Atlas.Domain.LowCode.Entities;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.LowCode;

/// <summary>
/// Yjs 协同离线快照作业（M16 S16-2 收尾）。
///
/// 周期：每 10 分钟。
/// 行为：把 LowCodeCollabSnapshotCache 内最新 update（base64）落到 AppVersionArchive，
///       label = "collab-snapshot-{tenantId}-{appId}-{ts}"，isSystemSnapshot=true。
///
/// 强约束：
/// - 服务端不解析 Yjs CRDT 内部结构；快照内容存原始 base64 update。
/// - 与 PLAN.md §M16 S16-2 完全对齐。
/// - 落表后清空缓存项，避免重复快照。
/// </summary>
public sealed class LowCodeCollabSnapshotJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LowCodeCollabSnapshotJob> _logger;

    public LowCodeCollabSnapshotJob(IServiceScopeFactory scopeFactory, ILogger<LowCodeCollabSnapshotJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    [AutomaticRetry(Attempts = 2)]
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var snapshot = LowCodeCollabSnapshotCache.Snapshot();
        if (snapshot.Count == 0)
        {
            _logger.LogTrace("LowCodeCollabSnapshotJob: no pending updates");
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var versionRepo = scope.ServiceProvider.GetRequiredService<IAppVersionArchiveRepository>();
        var auditWriter = scope.ServiceProvider.GetRequiredService<IAuditWriter>();
        var idGen = scope.ServiceProvider.GetRequiredService<IIdGeneratorAccessor>();

        var ts = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
        var processed = 0;
        foreach (var (key, base64Update) in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // key = "{tenantId}:{appId}"
            var sep = key.IndexOf(':');
            if (sep <= 0) continue;
            if (!Guid.TryParse(key[..sep], out var tenantGuid)) continue;
            var appCode = key[(sep + 1)..];
            // appCode 在 collab Hub 设计为客户端传入的 appId 字符串；尝试解析为 long（与 AppDefinition.Id 对齐）。
            if (!long.TryParse(appCode, out var appIdLong))
            {
                _logger.LogTrace("collab snapshot: skip non-numeric appId {AppId}", appCode);
                continue;
            }

            var tenantId = new TenantId(tenantGuid);
            var label = $"collab-snapshot-{ts}";
            var resourceJson = "{}";
            var schemaSnapshot = $"{{\"yjsUpdate\":\"{base64Update}\",\"snapshottedAt\":\"{DateTimeOffset.UtcNow:O}\"}}";
            var versionId = idGen.NextId();
            var archive = new AppVersionArchive(
                tenantId, versionId, appIdLong,
                label, schemaSnapshot, resourceJson,
                note: "Yjs 协同周期快照",
                createdByUserId: 0L, // 系统
                isSystemSnapshot: true);
            try
            {
                await versionRepo.InsertAsync(archive, cancellationToken);
                LowCodeCollabSnapshotCache.Clear(tenantGuid, appCode);
                processed++;
                await auditWriter.WriteAsync(new AuditRecord(
                    tenantId, "0", "lowcode.collab.snapshot", "success",
                    $"app:{appIdLong}:version:{versionId}:label:{label}",
                    null, null), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "collab snapshot insert failed tenant={Tenant} app={App}", tenantGuid, appIdLong);
            }
        }

        _logger.LogInformation("LowCodeCollabSnapshotJob: processed={Count}/{Total}", processed, snapshot.Count);
    }
}

/// <summary>注册 Hangfire 周期任务（每 10 分钟）。AppHost 启动时调用。</summary>
public sealed class LowCodeCollabSnapshotSchedulerHostedService : IHostedService
{
    private readonly IRecurringJobManager _recurring;

    public LowCodeCollabSnapshotSchedulerHostedService(IRecurringJobManager recurring)
    {
        _recurring = recurring;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _recurring.AddOrUpdate<LowCodeCollabSnapshotJob>(
            recurringJobId: "lowcode-collab-snapshot",
            methodCall: job => job.RunAsync(CancellationToken.None),
            cronExpression: "*/10 * * * *");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
