namespace Atlas.Core.Governance;

public interface ICanaryReleaseService
{
    Task<bool> IsEnabledForTenantAsync(string featureKey, string tenantId, CancellationToken ct);
    Task SetRolloutPercentageAsync(string featureKey, int percentage, CancellationToken ct);
    Task<CanaryReleaseInfo> GetInfoAsync(string featureKey, CancellationToken ct);
    Task<IReadOnlyList<CanaryReleaseInfo>> ListAllAsync(CancellationToken ct);
}

public sealed record CanaryReleaseInfo(string FeatureKey, int RolloutPercentage, bool IsActive, DateTime? ActivatedAt);
