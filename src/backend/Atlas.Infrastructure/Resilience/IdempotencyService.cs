using Atlas.Application.Resilience;
using Atlas.Core.Abstractions;
using Atlas.Core.Messaging;
using SqlSugar;

namespace Atlas.Infrastructure.Resilience;

public sealed class IdempotencyService : IIdempotencyService
{
    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGen;

    public IdempotencyService(ISqlSugarClient db, IIdGeneratorAccessor idGen)
    {
        _db = db;
        _idGen = idGen;
    }

    public async Task<IdempotencyCheckResult> IsProcessedAsync(
        string tenantId,
        string userId,
        string apiName,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(tenantId, out var tenantGuid))
            throw new ArgumentException("tenantId must be a GUID string.", nameof(tenantId));

        var rows = await _db.Queryable<IdempotencyRecord>()
            .Where(x => x.TenantId == tenantGuid
                && x.UserId == userId
                && x.ApiName == apiName
                && x.IdempotencyKey == idempotencyKey)
            .Take(1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var row = rows.Count > 0 ? rows[0] : null;

        return row is null
            ? new IdempotencyCheckResult(false, null)
            : new IdempotencyCheckResult(true, row.ResponseJson);
    }

    public async Task RecordAsync(
        string tenantId,
        string userId,
        string apiName,
        string idempotencyKey,
        string responseJson,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(tenantId, out var tenantGuid))
            throw new ArgumentException("tenantId must be a GUID string.", nameof(tenantId));

        var row = new IdempotencyRecord
        {
            Id = _idGen.NextId(),
            TenantId = tenantGuid,
            UserId = userId,
            ApiName = apiName,
            IdempotencyKey = idempotencyKey,
            ResponseJson = string.IsNullOrWhiteSpace(responseJson) ? "{}" : responseJson,
            CreatedAt = DateTimeOffset.UtcNow
        };

        try
        {
            await _db.Insertable(row).ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqlSugarException ex) when (IsUniqueConstraint(ex))
        {
        }
    }

    private static bool IsUniqueConstraint(SqlSugarException ex)
    {
        var message = ex.Message ?? string.Empty;
        return message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
            || message.Contains("constraint failed", StringComparison.OrdinalIgnoreCase);
    }
}
