namespace Atlas.Core.Governance;

public interface IQuotaService
{
    Task<QuotaInfo> GetQuotaAsync(string tenantId, string resourceType, CancellationToken ct);
    Task<IReadOnlyList<QuotaInfo>> ListQuotasAsync(string tenantId, CancellationToken ct);
    Task<bool> TryConsumeAsync(string tenantId, string resourceType, int amount, CancellationToken ct);
    Task ResetAsync(string tenantId, string resourceType, CancellationToken ct);
}

public sealed record QuotaInfo(string ResourceType, int Limit, int Used, int Remaining);
