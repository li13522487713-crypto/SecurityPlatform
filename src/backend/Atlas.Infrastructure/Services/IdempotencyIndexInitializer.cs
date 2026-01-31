using Atlas.Domain.Identity.Entities;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

public sealed class IdempotencyIndexInitializer
{
    private readonly ISqlSugarClient _db;
    private readonly ILogger<IdempotencyIndexInitializer> _logger;

    public IdempotencyIndexInitializer(
        ISqlSugarClient db,
        ILogger<IdempotencyIndexInitializer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await CreateUniqueIndexIfNotExistsAsync(
                nameof(IdempotencyRecord),
                "UX_IdempotencyRecord_Tenant_User_Api_Key",
                $"{nameof(IdempotencyRecord.TenantIdValue)}, {nameof(IdempotencyRecord.UserId)}, {nameof(IdempotencyRecord.ApiName)}, {nameof(IdempotencyRecord.IdempotencyKey)}",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "幂等记录索引初始化失败");
        }
    }

    private async Task CreateUniqueIndexIfNotExistsAsync(
        string tableName,
        string indexName,
        string columns,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var exists = await _db.Ado.GetDataTableAsync(
            "SELECT name FROM sqlite_master WHERE type='index' AND name=@indexName",
            new { indexName });

        if (exists.Rows.Count > 0)
        {
            _logger.LogDebug("索引已存在，跳过：{IndexName} on {TableName}", indexName, tableName);
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var sql = $"CREATE UNIQUE INDEX {indexName} ON {tableName} ({columns})";
        await _db.Ado.ExecuteCommandAsync(sql);
        _logger.LogDebug("已创建索引：{IndexName} on {TableName}", indexName, tableName);
    }
}
