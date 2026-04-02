namespace Atlas.Core.Governance;

public interface IVersionFreezeService
{
    Task<bool> IsFrozenAsync(string resourceType, long resourceId, CancellationToken ct);
    Task FreezeAsync(string resourceType, long resourceId, string reason, string userId, CancellationToken ct);
    Task UnfreezeAsync(string resourceType, long resourceId, string userId, CancellationToken ct);
    Task<VersionFreezeInfo?> GetFreezeInfoAsync(string resourceType, long resourceId, CancellationToken ct);
    Task<IReadOnlyList<VersionFreezeInfo>> QueryFreezesAsync(string? resourceType, long? resourceId, CancellationToken ct);
}

public sealed record VersionFreezeInfo(string ResourceType, long ResourceId, string Reason, string FrozenBy, DateTime FrozenAt);
