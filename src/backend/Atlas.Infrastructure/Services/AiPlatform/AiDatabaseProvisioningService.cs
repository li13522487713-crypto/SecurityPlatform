using System.Data.Common;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services.DatabaseStructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class AiDatabaseProvisioningService : IAiDatabaseProvisioningService
{
    private readonly AiDatabaseRepository _databaseRepository;
    private readonly AiDatabaseHostProfileRepository _profileRepository;
    private readonly AiDatabasePhysicalInstanceRepository _instanceRepository;
    private readonly IAiDatabaseSecretProtector _secretProtector;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<AiDatabaseProvisioningService> _logger;

    public AiDatabaseProvisioningService(
        AiDatabaseRepository databaseRepository,
        AiDatabaseHostProfileRepository profileRepository,
        AiDatabasePhysicalInstanceRepository instanceRepository,
        IAiDatabaseSecretProtector secretProtector,
        IIdGeneratorAccessor idGeneratorAccessor,
        IHostEnvironment hostEnvironment,
        ILogger<AiDatabaseProvisioningService> logger)
    {
        _databaseRepository = databaseRepository;
        _profileRepository = profileRepository;
        _instanceRepository = instanceRepository;
        _secretProtector = secretProtector;
        _idGeneratorAccessor = idGeneratorAccessor;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public async Task ProvisionAsync(AiDatabase database, CancellationToken cancellationToken)
    {
        await ProvisionDraftAsync(database, cancellationToken);
        if (database.OnlineInstanceId.HasValue)
        {
            await ProvisionOnlineAsync(database, cancellationToken);
        }

        database.MarkProvisionReady();
        await _databaseRepository.UpdateAsync(database, cancellationToken);
    }

    public async Task ProvisionDraftAsync(AiDatabase database, CancellationToken cancellationToken)
        => await ProvisionEnvironmentAsync(database, AiDatabaseRecordEnvironment.Draft, cancellationToken);

    public async Task ProvisionOnlineAsync(AiDatabase database, CancellationToken cancellationToken)
        => await ProvisionEnvironmentAsync(database, AiDatabaseRecordEnvironment.Online, cancellationToken);

    public async Task ReProvisionDraftAsync(AiDatabase database, CancellationToken cancellationToken)
    {
        var instance = await EnsureInstanceAsync(database, AiDatabaseRecordEnvironment.Draft, cancellationToken);
        instance.MarkPending();
        await _instanceRepository.UpdateAsync(instance, cancellationToken);
        await ProvisionDraftAsync(database, cancellationToken);
    }

    public async Task<AiDatabaseConnectionTestResult> TestInstanceConnectionAsync(
        TenantId tenantId,
        string instanceId,
        CancellationToken cancellationToken)
    {
        var id = ParseId(instanceId, nameof(instanceId));
        var instance = await _instanceRepository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("物理实例不存在。", ErrorCodes.NotFound);
        var connectionString = _secretProtector.Decrypt(instance.EncryptedConnection);
        try
        {
            using var client = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = connectionString,
                DbType = DataSourceDriverRegistry.ResolveDbType(instance.DriverCode),
                IsAutoCloseConnection = true
            });
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

    public async Task DropInstanceAsync(TenantId tenantId, string instanceId, CancellationToken cancellationToken)
    {
        var id = ParseId(instanceId, nameof(instanceId));
        var instance = await _instanceRepository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("物理实例不存在。", ErrorCodes.NotFound);
        var profile = await _profileRepository.FindByIdAsync(tenantId, instance.HostProfileId, cancellationToken)
            ?? throw new BusinessException("托管配置不存在。", ErrorCodes.NotFound);
        await DropPhysicalAsync(profile, instance, cancellationToken);
        await _instanceRepository.DeleteAsync(instance, cancellationToken);
    }

    private async Task ProvisionEnvironmentAsync(
        AiDatabase database,
        AiDatabaseRecordEnvironment environment,
        CancellationToken cancellationToken)
    {
        var profileId = database.DefaultHostProfileId
            ?? throw new BusinessException("AI 数据库缺少托管配置。", ErrorCodes.ValidationError);
        var profile = await _profileRepository.FindByIdAsync(database.TenantId, profileId, cancellationToken)
            ?? throw new BusinessException("托管配置不存在。", ErrorCodes.NotFound);
        if (!profile.IsEnabled)
        {
            throw new BusinessException("托管配置已停用。", ErrorCodes.ValidationError);
        }

        var instance = await EnsureInstanceAsync(database, environment, cancellationToken);
        try
        {
            var provisioned = profile.DriverCode switch
            {
                "SQLite" => await ProvisionSqliteAsync(profile, database, environment, cancellationToken),
                "MySql" => await ProvisionMySqlAsync(profile, database, environment, cancellationToken),
                "PostgreSQL" => await ProvisionPostgreSqlAsync(profile, database, environment, cancellationToken),
                _ when profile.ProvisionMode == AiDatabaseProvisionMode.ExistingDatabase => ResolveExistingConnection(profile, database, environment),
                _ => throw new NotSupportedException($"{profile.DriverCode} does not support automatic AI database provisioning.")
            };

            instance.Configure(
                provisioned.DatabaseName,
                provisioned.SchemaName,
                provisioned.StoragePath,
                _secretProtector.Encrypt(provisioned.ConnectionString),
                provisioned.Charset,
                provisioned.Collation);
            await _instanceRepository.UpdateAsync(instance, cancellationToken);
        }
        catch (Exception ex)
        {
            var safe = _secretProtector.MaskConnectionString(ex.Message);
            instance.MarkFailed(safe);
            database.MarkProvisionFailed(safe);
            await _instanceRepository.UpdateAsync(instance, cancellationToken);
            await _databaseRepository.UpdateAsync(database, cancellationToken);
            _logger.LogError(ex, "AI database managed provisioning failed db={DatabaseId} env={Environment}: {Message}", database.Id, environment, safe);
            throw;
        }
    }

    private async Task<AiDatabasePhysicalInstance> EnsureInstanceAsync(
        AiDatabase database,
        AiDatabaseRecordEnvironment environment,
        CancellationToken cancellationToken)
    {
        var existing = await _instanceRepository.FindByDatabaseEnvironmentAsync(database.TenantId, database.Id, environment, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var profileId = database.DefaultHostProfileId
            ?? throw new BusinessException("AI 数据库缺少托管配置。", ErrorCodes.ValidationError);
        var instance = new AiDatabasePhysicalInstance(
            database.TenantId,
            _idGeneratorAccessor.NextId(),
            database.Id,
            environment,
            database.DriverCode,
            profileId);
        await _instanceRepository.AddAsync(instance, cancellationToken);
        if (environment == AiDatabaseRecordEnvironment.Draft)
        {
            database.ConfigureManagedInstances(database.DriverCode, profileId, instance.Id, database.OnlineInstanceId, database.DraftDatabaseName, database.OnlineDatabaseName);
        }
        else
        {
            database.ConfigureManagedInstances(database.DriverCode, profileId, database.DraftInstanceId, instance.Id, database.DraftDatabaseName, database.OnlineDatabaseName);
        }

        await _databaseRepository.UpdateAsync(database, cancellationToken);
        return instance;
    }

    private Task<ProvisionedConnection> ProvisionSqliteAsync(
        AiDatabaseHostProfile profile,
        AiDatabase database,
        AiDatabaseRecordEnvironment environment,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var root = ResolveSqliteRoot(profile);
        Directory.CreateDirectory(root);
        var databaseName = PhysicalName(database, environment);
        var path = Path.Combine(root, $"{databaseName}.db");
        using var stream = File.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        return Task.FromResult(new ProvisionedConnection(databaseName, null, path, $"Data Source={path};", null, null));
    }

    private async Task<ProvisionedConnection> ProvisionMySqlAsync(
        AiDatabaseHostProfile profile,
        AiDatabase database,
        AiDatabaseRecordEnvironment environment,
        CancellationToken cancellationToken)
    {
        var databaseName = PhysicalName(database, environment);
        var charset = string.IsNullOrWhiteSpace(profile.DefaultCharset) ? "utf8mb4" : profile.DefaultCharset;
        var collation = string.IsNullOrWhiteSpace(profile.DefaultCollation) ? "utf8mb4_0900_ai_ci" : profile.DefaultCollation;
        ValidateSqlToken(charset, nameof(profile.DefaultCharset));
        ValidateSqlToken(collation, nameof(profile.DefaultCollation));
        var admin = RequiredConnection(profile);
        await ExecuteAdminAsync(profile.DriverCode, admin, $"CREATE DATABASE IF NOT EXISTS `{databaseName}` CHARACTER SET {charset} COLLATE {collation};", cancellationToken);
        return new ProvisionedConnection(databaseName, null, null, WithDatabase(admin, databaseName), charset, collation);
    }

    private async Task<ProvisionedConnection> ProvisionPostgreSqlAsync(
        AiDatabaseHostProfile profile,
        AiDatabase database,
        AiDatabaseRecordEnvironment environment,
        CancellationToken cancellationToken)
    {
        var admin = RequiredConnection(profile);
        var name = PhysicalName(database, environment);
        ValidateSqlToken(name, nameof(name));
        if (profile.ProvisionMode == AiDatabaseProvisionMode.PostgreSqlDatabase)
        {
            await ExecuteAdminAsync(profile.DriverCode, admin, $"""CREATE DATABASE "{name}";""", cancellationToken, ignoreAlreadyExists: true);
            return new ProvisionedConnection(name, null, null, WithDatabase(admin, name), null, null);
        }

        await ExecuteAdminAsync(profile.DriverCode, admin, $"""CREATE SCHEMA IF NOT EXISTS "{name}";""", cancellationToken);
        return new ProvisionedConnection(GetDatabaseName(admin), name, null, admin, null, null);
    }

    private ProvisionedConnection ResolveExistingConnection(
        AiDatabaseHostProfile profile,
        AiDatabase database,
        AiDatabaseRecordEnvironment environment)
    {
        var connection = RequiredConnection(profile);
        var databaseName = GetDatabaseName(connection);
        var schema = string.IsNullOrWhiteSpace(profile.DefaultSchema) ? PhysicalName(database, environment) : profile.DefaultSchema;
        ValidateSqlToken(schema, nameof(profile.DefaultSchema));
        return new ProvisionedConnection(databaseName, schema, null, connection, profile.DefaultCharset, profile.DefaultCollation);
    }

    private async Task DropPhysicalAsync(AiDatabaseHostProfile profile, AiDatabasePhysicalInstance instance, CancellationToken cancellationToken)
    {
        if (string.Equals(instance.DriverCode, "SQLite", StringComparison.OrdinalIgnoreCase))
        {
            var connection = _secretProtector.Decrypt(instance.EncryptedConnection);
            var path = connection.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase)
                ? connection["Data Source=".Length..].Trim().TrimEnd(';')
                : instance.StoragePath;
            var root = ResolveSqliteRoot(profile);
            var fullPath = Path.GetFullPath(path);
            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessException("SQLite 实例路径不在托管根目录内，拒绝删除。", ErrorCodes.ValidationError);
            }

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            return;
        }

        if (string.Equals(instance.DriverCode, "MySql", StringComparison.OrdinalIgnoreCase))
        {
            await ExecuteAdminAsync(profile.DriverCode, RequiredConnection(profile), $"DROP DATABASE IF EXISTS `{instance.PhysicalDatabaseName}`;", cancellationToken);
            return;
        }

        if (string.Equals(instance.DriverCode, "PostgreSQL", StringComparison.OrdinalIgnoreCase) &&
            profile.ProvisionMode == AiDatabaseProvisionMode.PostgreSqlSchema)
        {
            await ExecuteAdminAsync(profile.DriverCode, RequiredConnection(profile), $"""DROP SCHEMA IF EXISTS "{instance.PhysicalSchemaName}" CASCADE;""", cancellationToken);
        }
    }

    private async Task ExecuteAdminAsync(string driverCode, string connectionString, string sql, CancellationToken cancellationToken, bool ignoreAlreadyExists = false)
    {
        try
        {
            using var client = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = connectionString,
                DbType = DataSourceDriverRegistry.ResolveDbType(driverCode),
                IsAutoCloseConnection = true
            });
            client.Ado.CommandTimeOut = 30;
            await client.Ado.ExecuteCommandAsync(sql);
        }
        catch (Exception ex) when (ignoreAlreadyExists && ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
        }
    }

    private string RequiredConnection(AiDatabaseHostProfile profile)
    {
        var connection = _secretProtector.Decrypt(profile.EncryptedAdminConnection);
        if (string.IsNullOrWhiteSpace(connection))
        {
            throw new BusinessException("托管配置缺少 AdminConnection。", ErrorCodes.ValidationError);
        }

        return connection;
    }

    private string ResolveSqliteRoot(AiDatabaseHostProfile profile)
    {
        var configured = string.IsNullOrWhiteSpace(profile.SqliteRootPath) ? "data/ai-db" : profile.SqliteRootPath.Trim();
        if (Path.IsPathRooted(configured) || configured.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => part == ".."))
        {
            throw new BusinessException("SQLite 托管根目录必须是平台目录下的相对路径。", ErrorCodes.ValidationError);
        }

        return Path.IsPathRooted(configured)
            ? Path.GetFullPath(configured)
            : Path.GetFullPath(Path.Combine(_hostEnvironment.ContentRootPath, configured));
    }

    private static void ValidateSqlToken(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var text = value.Trim();
        if (text.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '_' or '-' or '.')))
        {
            throw new BusinessException($"{name} 包含非法字符。", ErrorCodes.ValidationError);
        }
    }

    private static string PhysicalName(AiDatabase database, AiDatabaseRecordEnvironment environment)
        => $"atlas_ai_{database.TenantId.Value:N}_{database.Id}_{environment.ToString().ToLowerInvariant()}";

    private static string WithDatabase(string connectionString, string databaseName)
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        builder["Database"] = databaseName;
        return builder.ConnectionString;
    }

    private static string GetDatabaseName(string connectionString)
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        foreach (var key in new[] { "Database", "Initial Catalog" })
        {
            if (builder.TryGetValue(key, out var value) && value is not null)
            {
                return value.ToString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static long ParseId(string value, string name)
        => long.TryParse(value, out var id) && id > 0
            ? id
            : throw new BusinessException($"{name} 必须是有效字符串 ID。", ErrorCodes.ValidationError);

    private sealed record ProvisionedConnection(
        string DatabaseName,
        string? SchemaName,
        string? StoragePath,
        string ConnectionString,
        string? Charset,
        string? Collation);
}
