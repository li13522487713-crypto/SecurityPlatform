using Atlas.Domain.Identity.Entities;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

public sealed class TableViewIndexInitializer
{
    private readonly ISqlSugarClient _db;
    private readonly ILogger<TableViewIndexInitializer> _logger;

    public TableViewIndexInitializer(ISqlSugarClient db, ILogger<TableViewIndexInitializer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await CreateUniqueIndexIfNotExistsAsync(
                nameof(TableView),
                "UX_TableView_Tenant_User_Table_Name",
                $"{nameof(TableView.TenantIdValue)}, {nameof(TableView.UserId)}, {nameof(TableView.TableKey)}, {nameof(TableView.Name)}",
                cancellationToken);

            await CreateUniqueIndexIfNotExistsAsync(
                nameof(UserTableViewDefault),
                "UX_UserTableViewDefault_Tenant_User_Table",
                $"{nameof(UserTableViewDefault.TenantIdValue)}, {nameof(UserTableViewDefault.UserId)}, {nameof(UserTableViewDefault.TableKey)}",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "表格视图索引初始化失败");
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
