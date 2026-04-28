using System.Data.Common;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services.DatabaseStructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlSugar;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class AiDatabaseProvisionService : IAiDatabaseProvisioner
{
    private readonly AiDatabaseRepository _repository;
    private readonly AiDatabaseHostingOptions _hostingOptions;
    private readonly DatabaseEncryptionOptions _encryptionOptions;
    private readonly IAiDatabaseProvisioningService? _managedProvisioningService;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<AiDatabaseProvisionService> _logger;

    public AiDatabaseProvisionService(
        AiDatabaseRepository repository,
        IOptions<AiDatabaseHostingOptions> hostingOptions,
        IOptions<DatabaseEncryptionOptions> encryptionOptions,
        IHostEnvironment hostEnvironment,
        ILogger<AiDatabaseProvisionService> logger)
        : this(repository, hostingOptions, encryptionOptions, null, hostEnvironment, logger)
    {
    }

    public AiDatabaseProvisionService(
        AiDatabaseRepository repository,
        IOptions<AiDatabaseHostingOptions> hostingOptions,
        IOptions<DatabaseEncryptionOptions> encryptionOptions,
        IAiDatabaseProvisioningService? managedProvisioningService,
        IHostEnvironment hostEnvironment,
        ILogger<AiDatabaseProvisionService> logger)
    {
        _repository = repository;
        _hostingOptions = hostingOptions.Value;
        _encryptionOptions = encryptionOptions.Value;
        _managedProvisioningService = managedProvisioningService;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public async Task EnsureProvisionedAsync(AiDatabase database, CancellationToken cancellationToken)
    {
        if (database.DefaultHostProfileId.HasValue)
        {
            if (database.ProvisionState == AiDatabaseProvisionState.Ready &&
                database.DraftInstanceId.HasValue &&
                database.OnlineInstanceId.HasValue)
            {
                return;
            }

            if (_managedProvisioningService is null)
            {
                throw new InvalidOperationException("Managed AI database provisioning service is not registered.");
            }

            await _managedProvisioningService.ProvisionAsync(database, cancellationToken);
            return;
        }

        if (database.StorageMode == AiDatabaseStorageMode.Standalone &&
            database.ProvisionState == AiDatabaseProvisionState.Ready &&
            !string.IsNullOrWhiteSpace(database.EncryptedDraftConnection) &&
            !string.IsNullOrWhiteSpace(database.EncryptedOnlineConnection))
        {
            return;
        }

        try
        {
            var driverCode = DataSourceDriverRegistry.NormalizeDriverCode(database.DriverCode);
            _ = DataSourceDriverRegistry.ResolveDbType(driverCode);
            var names = AiDatabasePhysicalNameBuilder.Build(database.TenantId, database.Id, driverCode);
            var connections = driverCode switch
            {
                "SQLite" => await ProvisionSqliteAsync(names, cancellationToken),
                "MySql" => await ProvisionMySqlAsync(names, cancellationToken),
                "PostgreSQL" => await ProvisionPostgreSqlAsync(names, cancellationToken),
                "SqlServer" or "Oracle" or "Dm" or "Kdbndp" or "Oscar" => throw new NotSupportedException($"{driverCode} does not support automatic AI database provisioning in this release."),
                _ => throw new NotSupportedException($"Database provider {driverCode} is not supported for AI database provisioning.")
            };

            database.ConfigureStandaloneStorage(
                driverCode,
                Protect(connections.DraftConnection),
                Protect(connections.OnlineConnection),
                names.LogicalName,
                names.DraftName,
                names.OnlineName,
                "v1");
            await _repository.UpdateAsync(database, cancellationToken);

            _logger.LogInformation(
                "Provisioned AI database {DatabaseId} tenant {TenantId} driver {DriverCode} draft {DraftName} online {OnlineName}.",
                database.Id,
                database.TenantId.Value,
                driverCode,
                names.DraftName,
                names.OnlineName);
        }
        catch (Exception ex)
        {
            var safeMessage = ConnectionStringMasker.Mask(ex.Message);
            database.MarkProvisionFailed(safeMessage);
            await _repository.UpdateAsync(database, cancellationToken);
            _logger.LogError(
                ex,
                "Failed to provision AI database {DatabaseId} tenant {TenantId}: {Message}",
                database.Id,
                database.TenantId.Value,
                safeMessage);
            throw;
        }
    }

    public Task EnsureDraftAsync(AiDatabase database, CancellationToken cancellationToken)
        => EnsureProvisionedAsync(database, cancellationToken);

    public Task EnsureOnlineAsync(AiDatabase database, CancellationToken cancellationToken)
        => EnsureProvisionedAsync(database, cancellationToken);

    public Task ValidateHostingOptionsAsync(string driverCode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = DataSourceDriverRegistry.NormalizeDriverCode(driverCode);
        _ = DataSourceDriverRegistry.ResolveDbType(normalized);
        if (string.Equals(normalized, "MySql", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(_hostingOptions.MySql.AdminConnection))
        {
            throw new InvalidOperationException("AiDatabaseHosting:MySql:AdminConnection is required for MySQL AI database provisioning.");
        }

        if (string.Equals(normalized, "PostgreSQL", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(_hostingOptions.PostgreSql.AdminConnection))
        {
            throw new InvalidOperationException("AiDatabaseHosting:PostgreSql:AdminConnection is required for PostgreSQL AI database provisioning.");
        }

        return Task.CompletedTask;
    }

    public async Task DropAsync(AiDatabase database, CancellationToken cancellationToken)
    {
        if (database.DefaultHostProfileId.HasValue)
        {
            if (database.DraftInstanceId.HasValue)
            {
                if (_managedProvisioningService is null)
                {
                    throw new InvalidOperationException("Managed AI database provisioning service is not registered.");
                }

                await _managedProvisioningService.DropInstanceAsync(database.TenantId, database.DraftInstanceId.Value.ToString(), cancellationToken);
            }

            if (database.OnlineInstanceId.HasValue)
            {
                if (_managedProvisioningService is null)
                {
                    throw new InvalidOperationException("Managed AI database provisioning service is not registered.");
                }

                await _managedProvisioningService.DropInstanceAsync(database.TenantId, database.OnlineInstanceId.Value.ToString(), cancellationToken);
            }

            return;
        }

        var driverCode = DataSourceDriverRegistry.NormalizeDriverCode(database.DriverCode);
        var names = AiDatabasePhysicalNameBuilder.Build(database.TenantId, database.Id, driverCode);
        switch (driverCode)
        {
            case "SQLite":
                DeleteSqliteFile(Unprotect(database.EncryptedDraftConnection));
                DeleteSqliteFile(Unprotect(database.EncryptedOnlineConnection));
                break;
            case "MySql":
                await ExecuteAdminCommandAsync(
                    DbType.MySql,
                    Required(_hostingOptions.MySql.AdminConnection, "AiDatabaseHosting:MySql:AdminConnection"),
                    $"DROP DATABASE IF EXISTS `{names.DraftName}`;",
                    cancellationToken);
                await ExecuteAdminCommandAsync(
                    DbType.MySql,
                    Required(_hostingOptions.MySql.AdminConnection, "AiDatabaseHosting:MySql:AdminConnection"),
                    $"DROP DATABASE IF EXISTS `{names.OnlineName}`;",
                    cancellationToken);
                break;
            case "PostgreSQL":
                await ExecuteAdminCommandAsync(
                    DbType.PostgreSQL,
                    Required(_hostingOptions.PostgreSql.AdminConnection, "AiDatabaseHosting:PostgreSql:AdminConnection"),
                    $"""DROP SCHEMA IF EXISTS "{names.DraftName}" CASCADE;""",
                    cancellationToken);
                await ExecuteAdminCommandAsync(
                    DbType.PostgreSQL,
                    Required(_hostingOptions.PostgreSql.AdminConnection, "AiDatabaseHosting:PostgreSql:AdminConnection"),
                    $"""DROP SCHEMA IF EXISTS "{names.OnlineName}" CASCADE;""",
                    cancellationToken);
                break;
            default:
                throw new NotSupportedException($"{driverCode} automatic AI database drop is not supported.");
        }
    }

    private async Task<(string DraftConnection, string OnlineConnection)> ProvisionSqliteAsync(
        AiDatabasePhysicalNames names,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var root = ResolveSqliteRoot();
        Directory.CreateDirectory(root);
        var draftPath = AiDatabasePhysicalNameBuilder.BuildSqlitePath(root, names.DraftFileName);
        var onlinePath = AiDatabasePhysicalNameBuilder.BuildSqlitePath(root, names.OnlineFileName);
        TouchSqliteFile(draftPath);
        TouchSqliteFile(onlinePath);
        return ($"Data Source={draftPath};", $"Data Source={onlinePath};");
    }

    private async Task<(string DraftConnection, string OnlineConnection)> ProvisionMySqlAsync(
        AiDatabasePhysicalNames names,
        CancellationToken cancellationToken)
    {
        var adminConnection = Required(_hostingOptions.MySql.AdminConnection, "AiDatabaseHosting:MySql:AdminConnection");
        var charset = string.IsNullOrWhiteSpace(_hostingOptions.MySql.Charset) ? "utf8mb4" : _hostingOptions.MySql.Charset.Trim();
        var collation = string.IsNullOrWhiteSpace(_hostingOptions.MySql.Collation) ? "utf8mb4_0900_ai_ci" : _hostingOptions.MySql.Collation.Trim();
        await ExecuteAdminCommandAsync(DbType.MySql, adminConnection, $"CREATE DATABASE IF NOT EXISTS `{names.DraftName}` CHARACTER SET {charset} COLLATE {collation};", cancellationToken);
        await ExecuteAdminCommandAsync(DbType.MySql, adminConnection, $"CREATE DATABASE IF NOT EXISTS `{names.OnlineName}` CHARACTER SET {charset} COLLATE {collation};", cancellationToken);
        return (
            WithDatabase(adminConnection, names.DraftName),
            WithDatabase(adminConnection, names.OnlineName));
    }

    private async Task<(string DraftConnection, string OnlineConnection)> ProvisionPostgreSqlAsync(
        AiDatabasePhysicalNames names,
        CancellationToken cancellationToken)
    {
        var adminConnection = Required(_hostingOptions.PostgreSql.AdminConnection, "AiDatabaseHosting:PostgreSql:AdminConnection");
        await ExecuteAdminCommandAsync(DbType.PostgreSQL, adminConnection, $"""CREATE SCHEMA IF NOT EXISTS "{names.DraftName}";""", cancellationToken);
        await ExecuteAdminCommandAsync(DbType.PostgreSQL, adminConnection, $"""CREATE SCHEMA IF NOT EXISTS "{names.OnlineName}";""", cancellationToken);
        return (adminConnection, adminConnection);
    }

    private async Task ExecuteAdminCommandAsync(DbType dbType, string connectionString, string sql, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var client = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = connectionString,
                DbType = dbType,
                IsAutoCloseConnection = true
            });
            client.Ado.CommandTimeOut = Math.Clamp(_hostingOptions.CommandTimeoutSeconds, 1, 60);
            await client.Ado.ExecuteCommandAsync(sql);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"AI database admin command failed. Connection={ConnectionStringMasker.Mask(connectionString)}; Error={ConnectionStringMasker.Mask(ex.Message)}",
                ex);
        }
    }

    private string ResolveSqliteRoot()
    {
        var configured = string.IsNullOrWhiteSpace(_hostingOptions.Sqlite.Root)
            ? "data/ai-db"
            : _hostingOptions.Sqlite.Root.Trim();
        return Path.IsPathRooted(configured)
            ? Path.GetFullPath(configured)
            : Path.GetFullPath(Path.Combine(_hostEnvironment.ContentRootPath, configured));
    }

    private string Protect(string connectionString)
        => _encryptionOptions.Enabled
            ? TenantDbConnectionFactory.Encrypt(connectionString, _encryptionOptions.Key)
            : connectionString;

    private string Unprotect(string encrypted)
        => _encryptionOptions.Enabled
            ? TenantDbConnectionFactory.Decrypt(encrypted, _encryptionOptions.Key)
            : encrypted;

    private static string Required(string? value, string name)
        => string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException($"{name} is required.") : value.Trim();

    private static string WithDatabase(string connectionString, string databaseName)
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        builder["Database"] = databaseName;
        return builder.ConnectionString;
    }

    private static void TouchSqliteFile(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = File.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
    }

    private static void DeleteSqliteFile(string connectionString)
    {
        var prefix = "Data Source=";
        if (!connectionString.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var path = connectionString[prefix.Length..].Trim().TrimEnd(';');
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
