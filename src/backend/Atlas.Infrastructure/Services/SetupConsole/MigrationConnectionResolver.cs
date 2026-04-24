using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Atlas.Application.SetupConsole.Abstractions;
using Atlas.Application.SetupConsole.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.Setup.Entities;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services.AiPlatform;
using Microsoft.Extensions.Options;
using SqlSugar;

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

    public MigrationConnectionResolver(
        ISqlSugarClient db,
        ITenantProvider tenantProvider,
        TenantDataSourceRepository tenantDataSourceRepository,
        IOptions<DatabaseEncryptionOptions> encryptionOptions,
        AiDatabasePhysicalTableService aiDatabasePhysicalTableService)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _tenantDataSourceRepository = tenantDataSourceRepository;
        _encryptionOptions = encryptionOptions.Value;
        _aiDatabasePhysicalTableService = aiDatabasePhysicalTableService;
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
