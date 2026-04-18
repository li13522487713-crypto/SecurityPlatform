using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;

namespace Atlas.Application.LowCode.Repositories;

public interface ILowCodeAssetUploadSessionRepository
{
    Task<long> InsertAsync(LowCodeAssetUploadSession session, CancellationToken cancellationToken);
    Task<LowCodeAssetUploadSession?> FindByTokenAsync(TenantId tenantId, string token, CancellationToken cancellationToken);
    Task<bool> UpdateAsync(LowCodeAssetUploadSession session, CancellationToken cancellationToken);
    /// <summary>批量回收过期会话（M10 GC）。</summary>
    Task<int> ExpireOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken);
}
