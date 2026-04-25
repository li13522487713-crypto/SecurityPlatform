using System.Data;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services.DatabaseStructure;
using SqlSugar;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class DatabaseManagementService : IDatabaseManagementService
{
    private readonly AiDatabaseRepository _databaseRepository;
    private readonly AiDatabasePhysicalInstanceRepository _instanceRepository;
    private readonly AiDatabaseHostProfileRepository _profileRepository;
    private readonly IAiDatabaseSecretProtector _secretProtector;
    private readonly IDatabaseDialectRegistry _dialects;

    public DatabaseManagementService(
        AiDatabaseRepository databaseRepository,
        AiDatabasePhysicalInstanceRepository instanceRepository,
        AiDatabaseHostProfileRepository profileRepository,
        IAiDatabaseSecretProtector secretProtector,
        IDatabaseDialectRegistry dialects)
    {
        _databaseRepository = databaseRepository;
        _instanceRepository = instanceRepository;
        _profileRepository = profileRepository;
        _secretProtector = secretProtector;
        _dialects = dialects;
    }

    public async Task<IReadOnlyList<DatabaseCenterSourceDto>> ListSourcesAsync(
        TenantId tenantId,
        string? keyword,
        string? workspaceId,
        string? driver,
        string? status,
        AiDatabaseRecordEnvironment? environment,
        CancellationToken cancellationToken)
    {
        var workspaceNumericId = ParseOptionalId(workspaceId, nameof(workspaceId));
        IReadOnlyList<Atlas.Domain.AiPlatform.Entities.AiDatabase> databases;
        try
        {
            var page = await _databaseRepository.GetPagedAsync(tenantId, keyword, workspaceNumericId, 1, 500, cancellationToken);
            databases = page.Items;
        }
        catch (Exception ex) when (ex.Message.Contains("no such column", StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<DatabaseCenterSourceDto>();
        }
        var result = new List<DatabaseCenterSourceDto>();
        foreach (var database in databases)
        {
            var instances = await _instanceRepository.ListByDatabaseAsync(tenantId, database.Id, cancellationToken);
            foreach (var instance in instances)
            {
                if (!string.IsNullOrWhiteSpace(driver) && !string.Equals(instance.DriverCode, driver, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (environment.HasValue && instance.Environment != environment.Value)
                {
                    continue;
                }

                var state = instance.ProvisionState.ToString();
                if (!string.IsNullOrWhiteSpace(status) && !state.Contains(status, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var profile = await _profileRepository.FindByIdAsync(tenantId, instance.HostProfileId, cancellationToken);
                result.Add(new DatabaseCenterSourceDto(
                    SourceId(instance.Id),
                    "AiDatabaseInstance",
                    $"{database.Name} ({instance.Environment})",
                    instance.DriverCode,
                    DriverIcon(instance.DriverCode),
                    BuildAddress(profile, instance),
                    state,
                    instance.Environment,
                    instance.Environment == AiDatabaseRecordEnvironment.Online,
                    instance.HostProfileId.ToString(),
                    profile?.Name,
                    string.IsNullOrWhiteSpace(instance.PhysicalDatabaseName) ? database.DraftDatabaseName : instance.PhysicalDatabaseName,
                    string.IsNullOrWhiteSpace(instance.PhysicalSchemaName) ? null : instance.PhysicalSchemaName,
                    instance.UpdatedAt,
                    database.Id.ToString(),
                    database.WorkspaceId.ToString()));
            }
        }

        return result;
    }

    public async Task<IReadOnlyList<DatabaseCenterSchemaDto>> ListSchemasAsync(TenantId tenantId, string sourceId, CancellationToken cancellationToken)
    {
        var (instance, client, dialect) = await OpenAsync(tenantId, sourceId, cancellationToken);
        var schemas = await LoadSchemasAsync(client, instance, cancellationToken);
        var result = new List<DatabaseCenterSchemaDto>();
        foreach (var schema in schemas)
        {
            var groups = new List<DatabaseCenterSchemaGroupDto>();
            foreach (var type in new[] { "table", "view", "procedure", "function", "trigger", "event" })
            {
                var objects = await LoadObjectsAsync(client, dialect, schema, type, cancellationToken);
                groups.Add(new DatabaseCenterSchemaGroupDto(type, DisplayGroup(type), objects.Count, objects));
            }

            result.Add(new DatabaseCenterSchemaDto(
                schema,
                schema,
                IsSystemSchema(schema),
                instance.Environment == AiDatabaseRecordEnvironment.Online || IsSystemSchema(schema),
                groups));
        }

        return result;
    }

    public async Task<DatabaseCenterInstanceSummaryDto> GetInstanceSummaryAsync(TenantId tenantId, string sourceId, CancellationToken cancellationToken)
    {
        var instanceId = ParseSourceId(sourceId);
        var instance = await _instanceRepository.FindByIdAsync(tenantId, instanceId, cancellationToken)
            ?? throw new BusinessException("数据源不存在。", ErrorCodes.NotFound);
        var database = await _databaseRepository.FindByIdAsync(tenantId, instance.AiDatabaseId, cancellationToken)
            ?? throw new BusinessException("AI 数据库不存在。", ErrorCodes.NotFound);
        var profile = await _profileRepository.FindByIdAsync(tenantId, instance.HostProfileId, cancellationToken);
        var connection = _secretProtector.Decrypt(instance.EncryptedConnection);
        return new DatabaseCenterInstanceSummaryDto(
            sourceId,
            database.Name,
            instance.Environment,
            instance.ProvisionState.ToString(),
            instance.DriverCode,
            instance.Charset,
            instance.Collation,
            instance.PhysicalDatabaseName,
            instance.PhysicalSchemaName,
            database.OwnerId?.ToString(),
            instance.CreatedAt,
            instance.UpdatedAt,
            instance.LastConnectedAt,
            instance.HostProfileId.ToString(),
            profile?.Name,
            _secretProtector.MaskConnectionString(connection),
            instance.Environment == AiDatabaseRecordEnvironment.Online);
    }

    public async Task<AiDatabaseConnectionTestResult> TestSourceAsync(TenantId tenantId, string sourceId, CancellationToken cancellationToken)
    {
        var (instance, client, _) = await OpenAsync(tenantId, sourceId, cancellationToken);
        try
        {
            await client.Ado.GetScalarAsync("SELECT 1");
            instance.MarkConnectionTest(true, "Connection succeeded.");
            await _instanceRepository.UpdateAsync(instance, cancellationToken);
            return new AiDatabaseConnectionTestResult(true, "Connection succeeded.", DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            var message = _secretProtector.MaskConnectionString(ex.Message);
            instance.MarkConnectionTest(false, message);
            await _instanceRepository.UpdateAsync(instance, cancellationToken);
            return new AiDatabaseConnectionTestResult(false, message, DateTime.UtcNow);
        }
    }

    public async Task<IReadOnlyList<DatabaseCenterConnectionLogDto>> ListConnectionLogsAsync(
        TenantId tenantId,
        string sourceId,
        CancellationToken cancellationToken)
    {
        var instanceId = ParseSourceId(sourceId);
        var instance = await _instanceRepository.FindByIdAsync(tenantId, instanceId, cancellationToken)
            ?? throw new BusinessException("数据源不存在。", ErrorCodes.NotFound);
        if (instance.LastConnectionTestMessage is null)
        {
            return Array.Empty<DatabaseCenterConnectionLogDto>();
        }

        return
        [
            new DatabaseCenterConnectionLogDto(
                $"{instance.Id}:{instance.UpdatedAt:O}",
                sourceId,
                !instance.LastConnectionTestMessage.Contains("failed", StringComparison.OrdinalIgnoreCase),
                instance.LastConnectionTestMessage,
                instance.UpdatedAt)
        ];
    }

    private async Task<(AiDatabasePhysicalInstance Instance, SqlSugarClient Client, IDatabaseDialect Dialect)> OpenAsync(
        TenantId tenantId,
        string sourceId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var instanceId = ParseSourceId(sourceId);
        var instance = await _instanceRepository.FindByIdAsync(tenantId, instanceId, cancellationToken)
            ?? throw new BusinessException("数据源不存在。", ErrorCodes.NotFound);
        if (instance.ProvisionState != AiDatabaseProvisionState.Ready)
        {
            throw new BusinessException("数据源尚未就绪。", ErrorCodes.ValidationError);
        }

        var connection = _secretProtector.Decrypt(instance.EncryptedConnection);
        var client = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = connection,
            DbType = DataSourceDriverRegistry.ResolveDbType(instance.DriverCode),
            IsAutoCloseConnection = true
        });
        return (instance, client, _dialects.Resolve(instance.DriverCode));
    }

    private static async Task<IReadOnlyList<string>> LoadSchemasAsync(
        SqlSugarClient client,
        AiDatabasePhysicalInstance instance,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.Equals(instance.DriverCode, "SQLite", StringComparison.OrdinalIgnoreCase))
        {
            return ["main"];
        }

        var sql = instance.DriverCode switch
        {
            "MySql" => "SELECT schema_name AS name FROM information_schema.schemata ORDER BY schema_name",
            "PostgreSQL" => "SELECT schema_name AS name FROM information_schema.schemata ORDER BY schema_name",
            _ => "SELECT CURRENT_SCHEMA AS name"
        };
        var table = await client.Ado.GetDataTableAsync(sql);
        return table.Rows.Cast<DataRow>().Select(row => row["name"]?.ToString() ?? string.Empty).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
    }

    private static async Task<IReadOnlyList<DatabaseCenterSchemaObjectDto>> LoadObjectsAsync(
        SqlSugarClient client,
        IDatabaseDialect dialect,
        string schema,
        string type,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var sql = type switch
            {
                "view" => dialect.GetViewListSql(schema == "main" ? null : schema),
                "procedure" => dialect.GetProcedureListSql(schema == "main" ? null : schema),
                "function" => dialect.GetFunctionListSql(schema == "main" ? null : schema),
                "trigger" => dialect.GetTriggerListSql(schema == "main" ? null : schema),
                "event" => dialect.GetEventListSql(schema == "main" ? null : schema),
                _ => dialect.GetTableListSql(schema == "main" ? null : schema)
            };
            var table = await client.Ado.GetDataTableAsync(sql);
            return table.Rows.Cast<DataRow>()
                .Select(row => new DatabaseCenterSchemaObjectDto(
                    Read(row, "name"),
                    Read(row, "object_type", type),
                    Read(row, "schema_name", schema),
                    type is "table" or "view",
                    type is "table" or "view"))
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .ToList();
        }
        catch
        {
            return Array.Empty<DatabaseCenterSchemaObjectDto>();
        }
    }

    private static string SourceId(long instanceId) => $"ai:{instanceId}";

    private static long ParseSourceId(string sourceId)
    {
        var raw = sourceId.StartsWith("ai:", StringComparison.OrdinalIgnoreCase) ? sourceId[3..] : sourceId;
        return long.TryParse(raw, out var id) && id > 0
            ? id
            : throw new BusinessException("sourceId 必须是有效字符串 ID。", ErrorCodes.ValidationError);
    }

    private static long? ParseOptionalId(string? value, string name)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : long.TryParse(value, out var id) && id > 0
                ? id
                : throw new BusinessException($"{name} 必须是有效字符串 ID。", ErrorCodes.ValidationError);

    private static string BuildAddress(AiDatabaseHostProfile? profile, AiDatabasePhysicalInstance instance)
    {
        if (profile is null)
        {
            return instance.PhysicalDatabaseName;
        }

        if (string.Equals(instance.DriverCode, "SQLite", StringComparison.OrdinalIgnoreCase))
        {
            return instance.StoragePath;
        }

        return profile.Port.HasValue ? $"{profile.Host}:{profile.Port}" : profile.Host;
    }

    private static string DriverIcon(string driverCode)
        => driverCode.ToLowerInvariant();

    private static string DisplayGroup(string type)
        => type switch
        {
            "table" => "表",
            "view" => "视图",
            "procedure" => "存储过程",
            "function" => "函数",
            "trigger" => "触发器",
            "event" => "事件",
            _ => type
        };

    private static bool IsSystemSchema(string schema)
        => schema is "information_schema" or "mysql" or "performance_schema" or "sys" or "pg_catalog";

    private static string Read(DataRow row, string column, string fallback = "")
        => row.Table.Columns.Contains(column) && row[column] != DBNull.Value ? row[column]?.ToString() ?? fallback : fallback;
}
