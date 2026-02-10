using Atlas.Application.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class RefreshTokenRepository : RepositoryBase<RefreshToken>, IRefreshTokenRepository
{
    public RefreshTokenRepository(ISqlSugarClient db) : base(db) { }

    public Task RevokeBySessionAsync(TenantId tenantId, long sessionId, DateTimeOffset revokedAt, CancellationToken cancellationToken)
    {
        return Db.Updateable<RefreshToken>()
            .SetColumns(x => x.RevokedAt == revokedAt)
            .Where(x => x.TenantIdValue == tenantId.Value && x.SessionId == sessionId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task<RefreshToken?> FindByHashAsync(TenantId tenantId, string tokenHash, CancellationToken cancellationToken)
    {
        return await Db.Queryable<RefreshToken>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TokenHash == tokenHash)
            .FirstAsync(cancellationToken);
    }
}
