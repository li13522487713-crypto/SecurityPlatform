using System.Text.Json;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class AiDatabasePhysicalTableService
{
    private const string RowIdColumn = "atlas_row_id";
    private const string DataJsonColumn = "atlas_data_json";
    private const string OwnerUserIdColumn = "atlas_owner_user_id";
    private const string CreatorUserIdColumn = "atlas_creator_user_id";
    private const string ChannelIdColumn = "atlas_channel_id";
    private const string CreatedAtColumn = "atlas_created_at";
    private const string UpdatedAtColumn = "atlas_updated_at";

    private readonly ISqlSugarClient _db;
    private readonly ILogger<AiDatabasePhysicalTableService> _logger;

    public AiDatabasePhysicalTableService(ISqlSugarClient db, ILogger<AiDatabasePhysicalTableService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public (string DraftTableName, string OnlineTableName) BuildTableNames(TenantId tenantId, long databaseId)
    {
        var tenantSegment = tenantId.Value.ToString("N");
        return (
            $"atlas_ai_db_{tenantSegment}_{databaseId}_draft",
            $"atlas_ai_db_{tenantSegment}_{databaseId}_online");
    }

    public async Task EnsureDatabaseTablesAsync(
        AiDatabase database,
        IReadOnlyCollection<AiDatabaseRecord>? legacyDraftRows,
        CancellationToken cancellationToken)
    {
        var tableNames = ResolveTableNames(database);
        cancellationToken.ThrowIfCancellationRequested();
        await EnsureTableAsync(tableNames.DraftTableName);
        cancellationToken.ThrowIfCancellationRequested();
        await EnsureTableAsync(tableNames.OnlineTableName);
        cancellationToken.ThrowIfCancellationRequested();

        if (legacyDraftRows is { Count: > 0 } &&
            await CountRowsAsync(tableNames.DraftTableName, cancellationToken) == 0)
        {
            foreach (var row in legacyDraftRows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await InsertRowAsync(
                    tableNames.DraftTableName,
                    new AiDatabasePhysicalRow(
                        row.Id,
                        row.DataJson,
                        row.OwnerUserId == 0 ? null : row.OwnerUserId,
                        row.CreatorUserId == 0 ? null : row.CreatorUserId,
                        string.IsNullOrWhiteSpace(row.ChannelId) ? null : row.ChannelId,
                        row.CreatedAt,
                        row.UpdatedAt),
                    cancellationToken);
            }

            _logger.LogInformation(
                "Migrated {Count} legacy ai database rows into draft table {TableName} for database {DatabaseId}.",
                legacyDraftRows.Count,
                tableNames.DraftTableName,
                database.Id);
        }
    }

    public async Task DropDatabaseTablesAsync(AiDatabase database, CancellationToken cancellationToken)
    {
        var tableNames = ResolveTableNames(database);
        cancellationToken.ThrowIfCancellationRequested();
        await DropTableIfExistsAsync(tableNames.DraftTableName);
        cancellationToken.ThrowIfCancellationRequested();
        await DropTableIfExistsAsync(tableNames.OnlineTableName);
    }

    public async Task<(IReadOnlyList<AiDatabasePhysicalRow> Items, long Total)> GetPagedRowsAsync(
        AiDatabase database,
        AiDatabaseRecordEnvironment environment,
        AiDatabaseAccessPolicy policy,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var rows = await ListRowsInternalAsync(database, environment, cancellationToken);
        var filtered = rows.Where(row => policy.IsRecordVisible(row.OwnerUserId, row.ChannelId)).ToList();
        var total = filtered.Count;
        var items = filtered
            .Skip(Math.Max(0, (pageIndex - 1) * pageSize))
            .Take(pageSize)
            .ToList();
        return (items, total);
    }

    public async Task<IReadOnlyList<AiDatabasePhysicalRow>> ListRowsAsync(
        AiDatabase database,
        AiDatabaseRecordEnvironment environment,
        AiDatabaseAccessPolicy policy,
        CancellationToken cancellationToken)
    {
        var rows = await ListRowsInternalAsync(database, environment, cancellationToken);
        return rows.Where(row => policy.IsRecordVisible(row.OwnerUserId, row.ChannelId)).ToList();
    }

    public async Task<long> InsertRowAsync(
        AiDatabase database,
        AiDatabaseRecordEnvironment environment,
        AiDatabasePhysicalRow row,
        CancellationToken cancellationToken)
    {
        var tableName = ResolveTableNames(database).Get(environment);
        await InsertRowAsync(tableName, row, cancellationToken);
        return row.Id;
    }

    public async Task InsertRowsAsync(
        AiDatabase database,
        AiDatabaseRecordEnvironment environment,
        IReadOnlyCollection<AiDatabasePhysicalRow> rows,
        CancellationToken cancellationToken)
    {
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await InsertRowAsync(database, environment, row, cancellationToken);
        }
    }

    public async Task<AiDatabasePhysicalRow?> FindRowAsync(
        AiDatabase database,
        AiDatabaseRecordEnvironment environment,
        long rowId,
        CancellationToken cancellationToken)
    {
        var tableName = ResolveTableNames(database).Get(environment);
        if (!_db.DbMaintenance.IsAnyTable(tableName, false))
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var rows = await _db.Ado.SqlQueryAsync<AiDatabasePhysicalRowData>(
            $"""
            SELECT
                {QuoteIdentifier(RowIdColumn)} AS Id,
                {QuoteIdentifier(DataJsonColumn)} AS DataJson,
                {QuoteIdentifier(OwnerUserIdColumn)} AS OwnerUserId,
                {QuoteIdentifier(CreatorUserIdColumn)} AS CreatorUserId,
                {QuoteIdentifier(ChannelIdColumn)} AS ChannelId,
                {QuoteIdentifier(CreatedAtColumn)} AS CreatedAt,
                {QuoteIdentifier(UpdatedAtColumn)} AS UpdatedAt
            FROM {QuoteIdentifier(tableName)}
            WHERE {QuoteIdentifier(RowIdColumn)} = @id;
            """,
            new SugarParameter("@id", rowId));
        return rows.Select(MapPhysicalRow).FirstOrDefault();
    }

    public async Task UpdateRowAsync(
        AiDatabase database,
        AiDatabaseRecordEnvironment environment,
        AiDatabasePhysicalRow row,
        CancellationToken cancellationToken)
    {
        var tableName = ResolveTableNames(database).Get(environment);
        cancellationToken.ThrowIfCancellationRequested();
        await _db.Ado.ExecuteCommandAsync(
            $"""
            UPDATE {QuoteIdentifier(tableName)}
            SET
                {QuoteIdentifier(DataJsonColumn)} = @dataJson,
                {QuoteIdentifier(OwnerUserIdColumn)} = @ownerUserId,
                {QuoteIdentifier(CreatorUserIdColumn)} = @creatorUserId,
                {QuoteIdentifier(ChannelIdColumn)} = @channelId,
                {QuoteIdentifier(UpdatedAtColumn)} = @updatedAt
            WHERE {QuoteIdentifier(RowIdColumn)} = @id;
            """,
            new SugarParameter("@dataJson", row.DataJson),
            new SugarParameter("@ownerUserId", row.OwnerUserId.HasValue ? row.OwnerUserId.Value : DBNull.Value),
            new SugarParameter("@creatorUserId", row.CreatorUserId.HasValue ? row.CreatorUserId.Value : DBNull.Value),
            new SugarParameter("@channelId", string.IsNullOrEmpty(row.ChannelId) ? DBNull.Value : row.ChannelId),
            new SugarParameter("@updatedAt", row.UpdatedAt.HasValue ? row.UpdatedAt.Value.ToString("O") : DateTime.UtcNow.ToString("O")),
            new SugarParameter("@id", row.Id));
    }

    public async Task DeleteRowAsync(
        AiDatabase database,
        AiDatabaseRecordEnvironment environment,
        long rowId,
        CancellationToken cancellationToken)
    {
        var tableName = ResolveTableNames(database).Get(environment);
        cancellationToken.ThrowIfCancellationRequested();
        await _db.Ado.ExecuteCommandAsync(
            $"""
            DELETE FROM {QuoteIdentifier(tableName)}
            WHERE {QuoteIdentifier(RowIdColumn)} = @id;
            """,
            new SugarParameter("@id", rowId));
    }

    public async Task<int> CountRowsAsync(AiDatabase database, AiDatabaseRecordEnvironment environment, CancellationToken cancellationToken)
    {
        var tableName = ResolveTableNames(database).Get(environment);
        return await CountRowsAsync(tableName, cancellationToken);
    }

    public async Task SyncDraftToOnlineAsync(AiDatabase database, CancellationToken cancellationToken)
    {
        var tables = ResolveTableNames(database);
        var draftRows = await ListRowsInternalAsync(database, AiDatabaseRecordEnvironment.Draft, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await DropTableIfExistsAsync(tables.OnlineTableName);
        cancellationToken.ThrowIfCancellationRequested();
        await EnsureTableAsync(tables.OnlineTableName);

        foreach (var row in draftRows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await InsertRowAsync(tables.OnlineTableName, row with { UpdatedAt = DateTime.UtcNow }, cancellationToken);
        }
    }

    private async Task<List<AiDatabasePhysicalRow>> ListRowsInternalAsync(
        AiDatabase database,
        AiDatabaseRecordEnvironment environment,
        CancellationToken cancellationToken)
    {
        var tableName = ResolveTableNames(database).Get(environment);
        if (!_db.DbMaintenance.IsAnyTable(tableName, false))
        {
            return [];
        }

        cancellationToken.ThrowIfCancellationRequested();
        var rows = await _db.Ado.SqlQueryAsync<AiDatabasePhysicalRowData>(
            $"""
            SELECT
                {QuoteIdentifier(RowIdColumn)} AS Id,
                {QuoteIdentifier(DataJsonColumn)} AS DataJson,
                {QuoteIdentifier(OwnerUserIdColumn)} AS OwnerUserId,
                {QuoteIdentifier(CreatorUserIdColumn)} AS CreatorUserId,
                {QuoteIdentifier(ChannelIdColumn)} AS ChannelId,
                {QuoteIdentifier(CreatedAtColumn)} AS CreatedAt,
                {QuoteIdentifier(UpdatedAtColumn)} AS UpdatedAt
            FROM {QuoteIdentifier(tableName)}
            ORDER BY {QuoteIdentifier(CreatedAtColumn)} DESC, {QuoteIdentifier(RowIdColumn)} DESC;
            """);
        return rows.Select(MapPhysicalRow).ToList();
    }

    private async Task<int> CountRowsAsync(string tableName, CancellationToken cancellationToken)
    {
        if (!_db.DbMaintenance.IsAnyTable(tableName, false))
        {
            return 0;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var result = await _db.Ado.GetScalarAsync(
            $"SELECT COUNT(1) FROM {QuoteIdentifier(tableName)};");
        return Convert.ToInt32(result ?? 0);
    }

    private async Task EnsureTableAsync(string tableName)
    {
        if (_db.DbMaintenance.IsAnyTable(tableName, false))
        {
            return;
        }

        await _db.Ado.ExecuteCommandAsync(
            $"""
            CREATE TABLE IF NOT EXISTS {QuoteIdentifier(tableName)} (
                {QuoteIdentifier(RowIdColumn)} INTEGER PRIMARY KEY,
                {QuoteIdentifier(DataJsonColumn)} TEXT NOT NULL,
                {QuoteIdentifier(OwnerUserIdColumn)} INTEGER NULL,
                {QuoteIdentifier(CreatorUserIdColumn)} INTEGER NULL,
                {QuoteIdentifier(ChannelIdColumn)} TEXT NULL,
                {QuoteIdentifier(CreatedAtColumn)} TEXT NOT NULL,
                {QuoteIdentifier(UpdatedAtColumn)} TEXT NULL
            );
            """);
        await _db.Ado.ExecuteCommandAsync(
            $"""
            CREATE INDEX IF NOT EXISTS {QuoteIdentifier($"{tableName}_user_idx")}
            ON {QuoteIdentifier(tableName)} ({QuoteIdentifier(OwnerUserIdColumn)});
            """);
        await _db.Ado.ExecuteCommandAsync(
            $"""
            CREATE INDEX IF NOT EXISTS {QuoteIdentifier($"{tableName}_channel_idx")}
            ON {QuoteIdentifier(tableName)} ({QuoteIdentifier(ChannelIdColumn)});
            """);
    }

    private async Task DropTableIfExistsAsync(string tableName)
    {
        if (_db.DbMaintenance.IsAnyTable(tableName, false))
        {
            await _db.Ado.ExecuteCommandAsync($"DROP TABLE {QuoteIdentifier(tableName)};");
        }
    }

    private async Task InsertRowAsync(string tableName, AiDatabasePhysicalRow row, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _db.Ado.ExecuteCommandAsync(
            $"""
            INSERT INTO {QuoteIdentifier(tableName)} (
                {QuoteIdentifier(RowIdColumn)},
                {QuoteIdentifier(DataJsonColumn)},
                {QuoteIdentifier(OwnerUserIdColumn)},
                {QuoteIdentifier(CreatorUserIdColumn)},
                {QuoteIdentifier(ChannelIdColumn)},
                {QuoteIdentifier(CreatedAtColumn)},
                {QuoteIdentifier(UpdatedAtColumn)}
            ) VALUES (
                @id,
                @dataJson,
                @ownerUserId,
                @creatorUserId,
                @channelId,
                @createdAt,
                @updatedAt
            );
            """,
            new SugarParameter("@id", row.Id),
            new SugarParameter("@dataJson", row.DataJson),
            new SugarParameter("@ownerUserId", row.OwnerUserId.HasValue ? row.OwnerUserId.Value : DBNull.Value),
            new SugarParameter("@creatorUserId", row.CreatorUserId.HasValue ? row.CreatorUserId.Value : DBNull.Value),
            new SugarParameter("@channelId", string.IsNullOrEmpty(row.ChannelId) ? DBNull.Value : row.ChannelId),
            new SugarParameter("@createdAt", row.CreatedAt.ToString("O")),
            new SugarParameter("@updatedAt", row.UpdatedAt.HasValue ? row.UpdatedAt.Value.ToString("O") : DBNull.Value));
    }

    private (string DraftTableName, string OnlineTableName) ResolveTableNames(AiDatabase database)
    {
        var draft = string.IsNullOrWhiteSpace(database.DraftTableName)
            ? BuildTableNames(database.TenantId, database.Id).DraftTableName
            : database.DraftTableName;
        var online = string.IsNullOrWhiteSpace(database.OnlineTableName)
            ? BuildTableNames(database.TenantId, database.Id).OnlineTableName
            : database.OnlineTableName;
        return (draft, online);
    }

    private static string QuoteIdentifier(string name)
        => $"\"{name.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static AiDatabasePhysicalRow MapPhysicalRow(AiDatabasePhysicalRowData row)
        => new(
            row.Id,
            row.DataJson ?? "{}",
            row.OwnerUserId,
            row.CreatorUserId,
            row.ChannelId,
            row.CreatedAt,
            row.UpdatedAt);
}

public sealed record AiDatabasePhysicalRow(
    long Id,
    string DataJson,
    long? OwnerUserId,
    long? CreatorUserId,
    string? ChannelId,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

internal sealed class AiDatabasePhysicalRowData
{
    public long Id { get; set; }

    public string? DataJson { get; set; }

    public long? OwnerUserId { get; set; }

    public long? CreatorUserId { get; set; }

    public string? ChannelId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

internal static class AiDatabasePhysicalTableNamesExtensions
{
    public static string Get(
        this (string DraftTableName, string OnlineTableName) tableNames,
        AiDatabaseRecordEnvironment environment)
        => environment == AiDatabaseRecordEnvironment.Online
            ? tableNames.OnlineTableName
            : tableNames.DraftTableName;
}
