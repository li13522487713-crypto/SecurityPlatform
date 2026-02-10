using Atlas.Application.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class AuthSessionRepository : RepositoryBase<AuthSession>, IAuthSessionRepository
{
    public AuthSessionRepository(ISqlSugarClient db) : base(db) { }

    public Task RevokeAsync(TenantId tenantId, long sessionId, DateTimeOffset revokedAt, CancellationToken cancellationToken)
    {
        return Db.Updateable<AuthSession>()
            .SetColumns(x => x.RevokedAt == revokedAt)
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == sessionId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task<int> CountActiveByUserIdAsync(TenantId tenantId, long userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        return await Db.Queryable<AuthSession>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.UserId == userId
                && x.RevokedAt == null
                && x.ExpiresAt > now)
            .CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuthSession>> QueryOldestActiveByUserIdAsync(
        TenantId tenantId,
        long userId,
        DateTimeOffset now,
        int count,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<AuthSession>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.UserId == userId
                && x.RevokedAt == null
                && x.ExpiresAt > now)
            .OrderBy(x => x.CreatedAt, OrderByType.Asc)
            .Take(count)
            .ToListAsync(cancellationToken);
    }
}
