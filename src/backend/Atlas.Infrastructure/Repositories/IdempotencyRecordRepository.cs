using Atlas.Application.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class IdempotencyRecordRepository : IIdempotencyRecordRepository
{
    private readonly ISqlSugarClient _db;

    public IdempotencyRecordRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<IdempotencyRecord?> FindActiveAsync(
        TenantId tenantId,
        long userId,
        string apiName,
        string idempotencyKey,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<IdempotencyRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.UserId == userId
                && x.ApiName == apiName
                && x.IdempotencyKey == idempotencyKey
                && x.ExpiresAt > now)
            .FirstAsync(cancellationToken);
    }

    public async Task<bool> TryAddAsync(IdempotencyRecord record, CancellationToken cancellationToken)
    {
        try
        {
            var rows = await _db.Insertable(record).ExecuteCommandAsync(cancellationToken);
            return rows > 0;
        }
        catch (SqlSugarException ex)
        {
            if (IsUniqueConstraint(ex))
            {
                return false;
            }

            throw;
        }
    }

    public Task UpdateAsync(IdempotencyRecord record, CancellationToken cancellationToken)
    {
        return _db.Updateable(record)
            .Where(x => x.Id == record.Id && x.TenantIdValue == record.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        return _db.Deleteable<IdempotencyRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        return await _db.Deleteable<IdempotencyRecord>()
            .Where(x => x.ExpiresAt <= now)
            .ExecuteCommandAsync(cancellationToken);
    }

    private static bool IsUniqueConstraint(SqlSugarException ex)
    {
        var message = ex.Message ?? string.Empty;
        return message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
            || message.Contains("constraint failed", StringComparison.OrdinalIgnoreCase);
    }
}
