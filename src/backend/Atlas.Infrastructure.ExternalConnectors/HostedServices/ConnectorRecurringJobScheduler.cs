using Atlas.Domain.ExternalConnectors.Entities;
using Hangfire;

namespace Atlas.Infrastructure.ExternalConnectors.HostedServices;

/// <summary>
/// 调度连接器 RecurringJob 的薄包装；统一 jobId 命名规则，便于运维侧查找。
/// </summary>
public static class ConnectorRecurringJobScheduler
{
    public static string BuildSyncJobId(Guid tenantId, long providerId)
        => $"connector-dir-sync:{tenantId:D}:{providerId}";

    public static void Schedule(IRecurringJobManager manager, ExternalIdentityProvider provider)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(provider);
        if (string.IsNullOrWhiteSpace(provider.SyncCron))
        {
            return;
        }

        var jobId = BuildSyncJobId(provider.TenantId.Value, provider.Id);
        var tenantId = provider.TenantId.Value;
        var providerId = provider.Id;
        manager.AddOrUpdate<ExternalDirectoryRecurringSyncRunner>(
            jobId,
            runner => runner.RunAsync(tenantId, providerId),
            provider.SyncCron);
    }

    public static void Remove(IRecurringJobManager manager, Guid tenantId, long providerId)
    {
        ArgumentNullException.ThrowIfNull(manager);
        manager.RemoveIfExists(BuildSyncJobId(tenantId, providerId));
    }
}
