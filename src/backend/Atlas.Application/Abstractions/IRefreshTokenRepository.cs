using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;

namespace Atlas.Application.Abstractions;

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken);
    Task<RefreshToken?> FindByHashAsync(TenantId tenantId, string tokenHash, CancellationToken cancellationToken);
    Task UpdateAsync(RefreshToken token, CancellationToken cancellationToken);
    Task RevokeByUserIdAsync(TenantId tenantId, long userId, DateTimeOffset revokedAt, CancellationToken cancellationToken);
    Task RevokeBySessionAsync(TenantId tenantId, long sessionId, DateTimeOffset revokedAt, CancellationToken cancellationToken);
}
