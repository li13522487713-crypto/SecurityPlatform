using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;

namespace Atlas.Application.Abstractions;

public interface IAuthSessionRepository
{
    Task AddAsync(AuthSession session, CancellationToken cancellationToken);
    Task<AuthSession?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
    Task UpdateAsync(AuthSession session, CancellationToken cancellationToken);
    Task RevokeAsync(TenantId tenantId, long sessionId, DateTimeOffset revokedAt, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the count of active (not revoked, not expired) sessions for a user.
    /// </summary>
    Task<int> CountActiveByUserIdAsync(TenantId tenantId, long userId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the oldest active sessions for a user, ordered by creation time ascending.
    /// </summary>
    Task<IReadOnlyList<AuthSession>> QueryOldestActiveByUserIdAsync(TenantId tenantId, long userId, DateTimeOffset now, int count, CancellationToken cancellationToken);
}
