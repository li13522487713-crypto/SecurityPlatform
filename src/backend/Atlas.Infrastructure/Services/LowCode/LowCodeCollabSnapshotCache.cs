using System.Collections.Concurrent;

namespace Atlas.Infrastructure.Services.LowCode;

/// <summary>
/// 协同最新 update 的内存缓存（M16）。
///
/// LowCodeCollabHub.SendUpdate 写入；LowCodeCollabSnapshotJob 周期消费 → 落 AppVersionArchive。
/// 放在 Atlas.Infrastructure 中以避免 Infrastructure → AppHost 反向依赖。
/// </summary>
public static class LowCodeCollabSnapshotCache
{
    private static readonly ConcurrentDictionary<string, string> Latest = new();

    private static string Key(Guid tenantId, string appId) => $"{tenantId}:{appId}";

    public static void Store(Guid tenantId, string appId, string base64Update)
    {
        Latest[Key(tenantId, appId)] = base64Update;
    }

    public static string? TryGet(Guid tenantId, string appId)
    {
        return Latest.TryGetValue(Key(tenantId, appId), out var v) ? v : null;
    }

    public static IReadOnlyDictionary<string, string> Snapshot()
    {
        return new Dictionary<string, string>(Latest);
    }

    public static void Clear(Guid tenantId, string appId)
    {
        Latest.TryRemove(Key(tenantId, appId), out _);
    }

    public static void ClearAll()
    {
        Latest.Clear();
    }
}
