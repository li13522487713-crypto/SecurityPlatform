using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.SetupConsole.Abstractions;
using Atlas.Application.SetupConsole.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.Setup.Entities;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services.AiPlatform;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using SqlSugar;

#pragma warning disable CS0618 // 迁移控制台按要求不改设计，保留旧 AI 数据库物理表兼容解析。
namespace Atlas.Infrastructure.Services.SetupConsole;

public sealed class MigrationConnectionResolver : IMigrationConnectionResolver
{
    private static readonly Regex PasswordRegex = new(
        @"(?i)(password|pwd)\s*=\s*[^;]+",
        RegexOptions.Compiled);

    private readonly ISqlSugarClient _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly TenantDataSourceRepository _tenantDataSourceRepository;
    private readonly DatabaseEncryptionOptions _encryptionOptions;
    private readonly AiDatabasePhysicalTableService _aiDatabasePhysicalTableService;
    private readonly AiDatabasePhysicalInstanceRepository _aiDatabasePhysicalInstanceRepository;
    private readonly IAiDatabaseSecretProtector _aiDatabaseSecretProtector;

    public MigrationConnectionResolver(
        ISqlSugarClient db,
        ITenantProvider tenantProvider,
        TenantDataSourceRepository tenantDataSourceRepository,
        IOptions<DatabaseEncryptionOptions> encryptionOptions,
        AiDatabasePhysicalTableService aiDatabasePhysicalTableService,
        AiDatabasePhysicalInstanceRepository aiDatabasePhysicalInstanceRepository,
        IAiDatabaseSecretProtector aiDatabaseSecretProtector)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _tenantDataSourceRepository = tenantDataSourceRepository;
        _encryptionOptions = encryptionOptions.Value;
        _aiDatabasePhysicalTableService = aiDatabasePhysicalTableService;
        _aiDatabasePhysicalInstanceRepository = aiDatabasePhysicalInstanceRepository;
        _aiDatabaseSecretProtector = aiDatabaseSecretProtector;
    }

    public async Task<ResolvedMigrationConnection> ResolveAsync(
        DbConnectionConfig config,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        var normalizedMode = NormalizeMode(config.Mode);
        return normalizedMode switch
        {
            DataMigrationConnectionModes.CurrentSystem => ResolveCurrentSystem(config),
            DataMigrationConnectionModes.CurrentSystemAiDatabase => await ResolveCurrentSystemAiDatabaseAsync(config, cancellationToken).ConfigureAwait(false),
            DataMigrationConnectionModes.SavedDataSource => await ResolveSavedDataSourceAsync(config, cancellationToken).ConfigureAwait(false),
            DataMigrationConnectionModes.ConnectionString => ResolveDirectConnection(config),
            DataMigrationConnectionModes.VisualConfig => ResolveVisualConnection(config),
            _ => throw new InvalidOperationException($"unsupported migration connection mode: {config.Mode}")
        };
    }

    public DbConnectionConfig Mask(DbConnectionConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var maskedConnectionString = string.IsNullOrWhiteSpace(config.ConnectionString)
            ? config.ConnectionString
            : PasswordRegex.Replace(config.ConnectionString, "$1=***");
        Dictionary<string, string>? maskedVisual = null;
        if (config.VisualConfig is { Count: > 0 })
        {
            maskedVisual = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in config.VisualConfig)
            {
                maskedVisual[key] = key.Contains("password", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(key, "pwd", StringComparison.OrdinalIgnoreCase)
                    ? "***"
                    : value;
            }
        }

        return config with
        {
            ConnectionString = maskedConnectionString,
            VisualConfig = maskedVisual
        };
    }

    private ResolvedMigrationConnection ResolveCurrentSystem(DbConnectionConfig config)
    {
        var connectionString = _db.CurrentConnectionConfig?.ConnectionString
            ?? throw new InvalidOperationException("current system database connection is unavailable");
        var dbType = _db.CurrentConnectionConfig?.DbType.ToString()
            ?? throw new InvalidOperationException("current system database type is unavailable");

        var resolvedDriver = DataSourceDriverRegistry.NormalizeDriverCode(
            string.IsNullOrWhiteSpace(config.DriverCode) ? dbType : config.DriverCode);
        _ = DataSourceDriverRegistry.ResolveDbType(resolvedDriver);
        return new ResolvedMigrationConnection(
            resolvedDriver,
            dbType,
            DataMigrationConnectionModes.CurrentSystem,
            connectionString,
            config.DisplayName ?? "Current system database",
            null,
            null,
            Array.Empty<ResolvedMigrationTable>(),
            ComputeFingerprint($"current-system|{dbType}|{connectionString}"));
    }

    private async Task<ResolvedMigrationConnection> ResolveCurrentSystemAiDatabaseAsync(
        DbConnectionConfig config,
        CancellationToken cancellationToken)
    {
        if (!config.AiDatabaseId.HasValue || config.AiDatabaseId.Value <= 0)
        {
            throw new InvalidOperationException("AiDatabaseId is required for CurrentSystemAiDatabase mode.");
        }

        var tenantId = _tenantProvider.GetTenantId();
        var database = await _db.Queryable<AiDatabase>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == config.AiDatabaseId.Value)
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);
        if (database is null)
        {
            throw new InvalidOperationException($"AiDatabase {config.AiDatabaseId.Value} not found.");
        }

        if (database.StorageMode == AiDatabaseStorageMode.Standalone
            && (database.DraftInstanceId.HasValue || database.OnlineInstanceId.HasValue))
        {
            return await ResolveStandaloneAiDatabaseAsync(config, tenantId, database, cancellationToken)
                .ConfigureAwait(false);
        }

        var baseConnection = ResolveCurrentSystem(config);
        var tableNames = _aiDatabasePhysicalTableService.BuildTableNames(tenantId, database.Id);
        var resolvedTables = new[]
        {
            new ResolvedMigrationTable(
                $"{database.Name} Draft",
                string.IsNullOrWhiteSpace(database.DraftTableName) ? tableNames.DraftTableName : database.DraftTableName,
                MigrationSqlSugarScopeFactory.AiRowIdColumn,
                true,
                null,
                "table"),
            new ResolvedMigrationTable(
                $"{database.Name} Online",
                string.IsNullOrWhiteSpace(database.OnlineTableName) ? tableNames.OnlineTableName : database.OnlineTableName,
                MigrationSqlSugarScopeFactory.AiRowIdColumn,
                true,
                null,
                "table")
        };

        return baseConnection with
        {
            Mode = DataMigrationConnectionModes.CurrentSystemAiDatabase,
            DisplayName = config.DisplayName ?? database.Name,
            AiDatabaseId = database.Id,
            Tables = resolvedTables,
            Fingerprint = ComputeFingerprint(
                $"current-ai-database|{tenantId.Value:D}|{database.Id}|{string.Join('|', resolvedTables.Select(x => x.TableName))}")
        };
    }

    private async Task<ResolvedMigrationConnection> ResolveStandaloneAiDatabaseAsync(
        DbConnectionConfig config,
        TenantId tenantId,
        AiDatabase database,
        CancellationToken cancellationToken)
    {
        var environment = ResolveAiDatabaseEnvironment(config.VisualConfig);
        var instance = await _aiDatabasePhysicalInstanceRepository
            .FindByDatabaseEnvironmentAsync(tenantId, database.Id, environment, cancellationToken)
            .ConfigureAwait(false);
        if (instance is null)
        {
            throw new InvalidOperationException($"AiDatabase {database.Id} {environment} physical instance not found.");
        }

        if (instance.ProvisionState != AiDatabaseProvisionState.Ready)
        {
            throw new InvalidOperationException($"AiDatabase {database.Id} {environment} physical instance is not ready.");
        }

        var connectionString = _aiDatabaseSecretProtector.Decrypt(instance.EncryptedConnection);
        var driverCode = DataSourceDriverRegistry.NormalizeDriverCode(instance.DriverCode);
        var tables = DiscoverStandaloneSqliteTables(connectionString, driverCode);
        return new ResolvedMigrationConnection(
            driverCode,
            driverCode,
            DataMigrationConnectionModes.CurrentSystemAiDatabase,
            connectionString,
            config.DisplayName ?? $"{database.Name} {environment}",
            null,
            database.Id,
            tables,
            ComputeFingerprint(
                $"standalone-ai-database|{tenantId.Value:D}|{database.Id}|{environment}|{connectionString}|{string.Join('|', tables.Select(x => x.TableName))}"));
    }

    private static AiDatabaseRecordEnvironment ResolveAiDatabaseEnvironment(IDictionary<string, string>? visualConfig)
    {
        if (visualConfig is not null
            && visualConfig.TryGetValue("environment", out var value)
            && Enum.TryParse<AiDatabaseRecordEnvironment>(value, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return AiDatabaseRecordEnvironment.Draft;
    }

    private static IReadOnlyList<ResolvedMigrationTable> DiscoverStandaloneSqliteTables(
        string connectionString,
        string driverCode)
    {
        if (!string.Equals(DataSourceDriverRegistry.NormalizeDriverCode(driverCode), "SQLite", StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<ResolvedMigrationTable>();
        }

        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        var tableNames = new List<string>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT name
                FROM sqlite_master
                WHERE type = 'table'
                  AND name NOT LIKE 'sqlite_%'
                ORDER BY name;
                """;
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var tableName = reader.GetString(0);
                if (!string.IsNullOrWhiteSpace(tableName))
                {
                    tableNames.Add(tableName);
                }
            }
        }

        var tables = new List<ResolvedMigrationTable>();
        foreach (var tableName in tableNames)
        {
            var primaryKeyColumn = ReadSqlitePrimaryKeyColumn(connection, tableName);

            if (string.IsNullOrWhiteSpace(primaryKeyColumn))
            {
                continue;
            }

            tables.Add(new ResolvedMigrationTable(tableName, tableName, primaryKeyColumn, true, null, "table"));
        }

        return tables;
    }

    private static string? ReadSqlitePrimaryKeyColumn(SqliteConnection connection, string tableName)
    {
        var fallbackIdColumn = default(string);
        var candidates = new List<(string Name, int Order)>();
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({MigrationSqlSugarScopeFactory.QuoteIdentifier("SQLite", tableName)});";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var name = reader.GetString(reader.GetOrdinal("name"));
            var primaryKeyOrder = reader.GetInt32(reader.GetOrdinal("pk"));
            if (primaryKeyOrder > 0)
            {
                candidates.Add((name, primaryKeyOrder));
            }
            else if (string.Equals(name, "id", StringComparison.OrdinalIgnoreCase))
            {
                fallbackIdColumn = name;
            }
        }

        return candidates
            .OrderBy(item => item.Order)
            .Select(item => item.Name)
            .FirstOrDefault()
            ?? fallbackIdColumn;
    }

    private async Task<ResolvedMigrationConnection> ResolveSavedDataSourceAsync(
        DbConnectionConfig config,
        CancellationToken cancellationToken)
    {
        if (!config.DataSourceId.HasValue || config.DataSourceId.Value <= 0)
        {
            throw new InvalidOperationException("DataSourceId is required for SavedDataSource mode.");
        }

        var tenantIdValue = _tenantProvider.GetTenantId().Value.ToString();
        var entity = await _tenantDataSourceRepository
            .FindByTenantAndIdAsync(tenantIdValue, config.DataSourceId.Value, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null || !entity.IsActive)
        {
            throw new InvalidOperationException($"TenantDataSource {config.DataSourceId.Value} not found or inactive.");
        }

        var connectionString = _encryptionOptions.Enabled
            ? TenantDbConnectionFactory.Decrypt(entity.EncryptedConnectionString, _encryptionOptions.Key)
            : entity.EncryptedConnectionString;
        var driverCode = DataSourceDriverRegistry.NormalizeDriverCode(entity.DbType);
        _ = DataSourceDriverRegistry.ResolveDbType(driverCode);

        return new ResolvedMigrationConnection(
            driverCode,
            driverCode,
            DataMigrationConnectionModes.SavedDataSource,
            connectionString,
            config.DisplayName ?? entity.Name,
            entity.Id,
            null,
            Array.Empty<ResolvedMigrationTable>(),
            ComputeFingerprint($"saved-datasource|{tenantIdValue}|{entity.Id}|{driverCode}|{connectionString}"));
    }

    private ResolvedMigrationConnection ResolveDirectConnection(DbConnectionConfig config)
    {
        var driverCode = DataSourceDriverRegistry.NormalizeDriverCode(config.DriverCode);
        var connectionString = config.ConnectionString?.Trim();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionString is required for ConnectionString mode.");
        }

        _ = DataSourceDriverRegistry.ResolveDbType(driverCode);
        return new ResolvedMigrationConnection(
            driverCode,
            driverCode,
            DataMigrationConnectionModes.ConnectionString,
            connectionString,
            config.DisplayName ?? driverCode,
            null,
            null,
            Array.Empty<ResolvedMigrationTable>(),
            ComputeFingerprint($"connection-string|{driverCode}|{connectionString}"));
    }

    private ResolvedMigrationConnection ResolveVisualConnection(DbConnectionConfig config)
    {
        var driverCode = DataSourceDriverRegistry.NormalizeDriverCode(config.DriverCode);
        var resolvedConnectionString = DataSourceDriverRegistry.ResolveConnectionString(
            driverCode,
            DataMigrationConnectionModes.Visual,
            null,
            config.VisualConfig is null ? null : new Dictionary<string, string>(config.VisualConfig, StringComparer.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(resolvedConnectionString))
        {
            throw new InvalidOperationException("VisualConfig is required for VisualConfig mode.");
        }

        _ = DataSourceDriverRegistry.ResolveDbType(driverCode);
        return new ResolvedMigrationConnection(
            driverCode,
            driverCode,
            DataMigrationConnectionModes.VisualConfig,
            resolvedConnectionString,
            config.DisplayName ?? driverCode,
            null,
            null,
            Array.Empty<ResolvedMigrationTable>(),
            ComputeFingerprint($"visual-config|{driverCode}|{resolvedConnectionString}"));
    }

    private static string NormalizeMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return DataMigrationConnectionModes.ConnectionString;
        }

        return mode.Trim() switch
        {
            "raw" => DataMigrationConnectionModes.ConnectionString,
            "visual" => DataMigrationConnectionModes.VisualConfig,
            var value => value
        };
    }

    private static string ComputeFingerprint(string canonical)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
