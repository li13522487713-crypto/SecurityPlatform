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

public sealed class AiDatabaseHostProfileService : IAiDatabaseHostProfileService
{
    private readonly AiDatabaseHostProfileRepository _repository;
    private readonly AiDatabasePhysicalInstanceRepository _instanceRepository;
    private readonly IAiDatabaseSecretProtector _secretProtector;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<AiDatabaseHostProfileService> _logger;

    public AiDatabaseHostProfileService(
        AiDatabaseHostProfileRepository repository,
        AiDatabasePhysicalInstanceRepository instanceRepository,
        IAiDatabaseSecretProtector secretProtector,
        IIdGeneratorAccessor idGeneratorAccessor,
        IHostEnvironment hostEnvironment,
        ILogger<AiDatabaseHostProfileService> logger)
    {
        _repository = repository;
        _instanceRepository = instanceRepository;
        _secretProtector = secretProtector;
        _idGeneratorAccessor = idGeneratorAccessor;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AiDatabaseHostProfileDto>> ListProfilesAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var items = await _repository.ListAsync(tenantId, cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task<AiDatabaseHostProfileDto?> GetProfileAsync(TenantId tenantId, string profileId, CancellationToken cancellationToken)
    {
        var id = ParseId(profileId, nameof(profileId));
        var entity = await _repository.FindByIdAsync(tenantId, id, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<AiDatabaseHostProfileDto> CreateProfileAsync(
        TenantId tenantId,
        AiDatabaseHostProfileCreateRequest request,
        string? operatorId,
        CancellationToken cancellationToken)
    {
        var driverCode = NormalizeDriver(request.DriverCode);
        ValidateProfileSettings(driverCode, request.ProvisionMode, request.DefaultCharset, request.DefaultCollation, request.DefaultSchema, request.SqliteRootPath);
        var entity = new AiDatabaseHostProfile(
            tenantId,
            _idGeneratorAccessor.NextId(),
            Required(request.Name, nameof(request.Name)),
            driverCode,
            request.ProvisionMode,
            operatorId);

        entity.Update(
            request.Name,
            driverCode,
            request.ProvisionMode,
            request.Host,
            request.Port,
            request.AdminDatabase,
            request.Username,
            _secretProtector.Encrypt(request.Password),
            _secretProtector.Encrypt(ResolveAdminConnection(driverCode, request)),
            request.DefaultCharset,
            request.DefaultCollation,
            request.DefaultSchema,
            request.SqliteRootPath,
            request.MaxDatabaseCount,
            request.IsEnabled,
            operatorId);
        entity.MarkDefault(request.IsDefault);

        if (entity.IsDefault)
        {
            await _repository.ClearDefaultAsync(tenantId, driverCode, entity.Id, cancellationToken);
        }

        await _repository.AddAsync(entity, cancellationToken);
        return Map(entity);
    }

    public async Task<AiDatabaseHostProfileDto> UpdateProfileAsync(
        TenantId tenantId,
        string profileId,
        AiDatabaseHostProfileUpdateRequest request,
        string? operatorId,
        CancellationToken cancellationToken)
    {
        var id = ParseId(profileId, nameof(profileId));
        var entity = await _repository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("托管配置不存在。", ErrorCodes.NotFound);

        var driverCode = NormalizeDriver(request.DriverCode);
        ValidateProfileSettings(driverCode, request.ProvisionMode, request.DefaultCharset, request.DefaultCollation, request.DefaultSchema, request.SqliteRootPath);
        var encryptedPassword = string.IsNullOrWhiteSpace(request.Password)
            ? entity.EncryptedPassword
            : _secretProtector.Encrypt(request.Password);
        var encryptedAdminConnection = string.IsNullOrWhiteSpace(request.AdminConnection) && !ShouldBuildConnection(driverCode, request)
            ? entity.EncryptedAdminConnection
            : _secretProtector.Encrypt(ResolveAdminConnection(driverCode, request));

        entity.Update(
            request.Name,
            driverCode,
            request.ProvisionMode,
            request.Host,
            request.Port,
            request.AdminDatabase,
            request.Username,
            encryptedPassword,
            encryptedAdminConnection,
            request.DefaultCharset,
            request.DefaultCollation,
            request.DefaultSchema,
            request.SqliteRootPath,
            request.MaxDatabaseCount,
            request.IsEnabled,
            operatorId);
        entity.MarkDefault(request.IsDefault);

        if (entity.IsDefault)
        {
            await _repository.ClearDefaultAsync(tenantId, driverCode, entity.Id, cancellationToken);
        }

        await _repository.UpdateAsync(entity, cancellationToken);
        return Map(entity);
    }

    public async Task DeleteProfileAsync(TenantId tenantId, string profileId, CancellationToken cancellationToken)
    {
        var id = ParseId(profileId, nameof(profileId));
        var entity = await _repository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("托管配置不存在。", ErrorCodes.NotFound);
        if (await _repository.HasInstancesAsync(tenantId, entity.Id, cancellationToken))
        {
            throw new BusinessException("托管配置已被数据库实例引用，不能删除。", ErrorCodes.ValidationError);
        }

        await _repository.DeleteAsync(entity, cancellationToken);
    }

    public async Task<AiDatabaseConnectionTestResult> TestProfileConnectionAsync(TenantId tenantId, string profileId, CancellationToken cancellationToken)
    {
        var id = ParseId(profileId, nameof(profileId));
        var entity = await _repository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("托管配置不存在。", ErrorCodes.NotFound);
        var result = await TestProfileAsync(entity, cancellationToken);
        entity.MarkTestResult(result.Success, result.Message);
        await _repository.UpdateAsync(entity, cancellationToken);
        return result;
    }

    public async Task SetDefaultProfileAsync(TenantId tenantId, string profileId, CancellationToken cancellationToken)
    {
        var id = ParseId(profileId, nameof(profileId));
        var entity = await _repository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("托管配置不存在。", ErrorCodes.NotFound);
        await _repository.ClearDefaultAsync(tenantId, entity.DriverCode, entity.Id, cancellationToken);
        entity.MarkDefault(true);
        await _repository.UpdateAsync(entity, cancellationToken);
    }

    public async Task<AiDatabaseHostProfileDto> ResolveDefaultProfileAsync(TenantId tenantId, string driverCode, CancellationToken cancellationToken)
    {
        var normalized = NormalizeDriver(driverCode);
        var entity = await _repository.FindDefaultAsync(tenantId, normalized, cancellationToken)
            ?? await _repository.FindEnabledByDriverAsync(tenantId, normalized, cancellationToken)
            ?? throw new BusinessException($"未配置 {normalized} 的 AI 数据库托管配置。", ErrorCodes.ValidationError);
        return Map(entity);
    }

    public Task<IReadOnlyList<AiDatabaseDriverDto>> GetAvailableDriversAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<AiDatabaseDriverDto> drivers =
        [
            new("SQLite", "SQLite", true, [AiDatabaseProvisionMode.SQLiteFile, AiDatabaseProvisionMode.ExistingDatabase]),
            new("MySql", "MySQL", true, [AiDatabaseProvisionMode.MySqlDatabase, AiDatabaseProvisionMode.ExistingDatabase]),
            new("PostgreSQL", "PostgreSQL", true, [AiDatabaseProvisionMode.PostgreSqlSchema, AiDatabaseProvisionMode.PostgreSqlDatabase, AiDatabaseProvisionMode.ExistingDatabase]),
            new("SqlServer", "SQL Server", false, [AiDatabaseProvisionMode.ExistingDatabase]),
            new("Oracle", "Oracle", false, [AiDatabaseProvisionMode.ExistingDatabase]),
            new("Dm", "达梦 DM", false, [AiDatabaseProvisionMode.ExistingDatabase]),
            new("Kdbndp", "Kingbase", false, [AiDatabaseProvisionMode.ExistingDatabase]),
            new("Oscar", "Oscar", false, [AiDatabaseProvisionMode.ExistingDatabase])
        ];
        return Task.FromResult(drivers);
    }

    private async Task<AiDatabaseConnectionTestResult> TestProfileAsync(AiDatabaseHostProfile profile, CancellationToken cancellationToken)
    {
        if (string.Equals(profile.DriverCode, "SQLite", StringComparison.OrdinalIgnoreCase))
        {
            var root = ResolveSqliteRoot(profile);
            Directory.CreateDirectory(root);
            return new AiDatabaseConnectionTestResult(true, "SQLite root ready.", DateTime.UtcNow);
        }

        var connectionString = _secretProtector.Decrypt(profile.EncryptedAdminConnection);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return new AiDatabaseConnectionTestResult(false, "Admin connection is empty.", DateTime.UtcNow);
        }

        try
        {
            using var client = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = connectionString,
                DbType = DataSourceDriverRegistry.ResolveDbType(profile.DriverCode),
                IsAutoCloseConnection = true
            });
            client.Ado.CommandTimeOut = 10;
            await client.Ado.GetScalarAsync("SELECT 1");
            return new AiDatabaseConnectionTestResult(true, "Connection succeeded.", DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            var message = _secretProtector.MaskConnectionString(ex.Message);
            _logger.LogWarning(ex, "AI database host profile test failed profile={ProfileId}: {Message}", profile.Id, message);
            return new AiDatabaseConnectionTestResult(false, message, DateTime.UtcNow);
        }
    }

    private AiDatabaseHostProfileDto Map(AiDatabaseHostProfile entity)
    {
        var connectionString = _secretProtector.Decrypt(entity.EncryptedAdminConnection);
        var summary = string.Equals(entity.DriverCode, "SQLite", StringComparison.OrdinalIgnoreCase)
            ? "sqlite://managed-root"
            : _secretProtector.MaskConnectionString(connectionString);
        return new AiDatabaseHostProfileDto(
            entity.Id.ToString(),
            entity.Name,
            entity.DriverCode,
            entity.ProvisionMode,
            string.IsNullOrWhiteSpace(entity.Host) ? null : entity.Host,
            entity.Port,
            string.IsNullOrWhiteSpace(entity.AdminDatabase) ? null : entity.AdminDatabase,
            string.IsNullOrWhiteSpace(entity.Username) ? null : entity.Username,
            string.IsNullOrWhiteSpace(entity.DefaultCharset) ? null : entity.DefaultCharset,
            string.IsNullOrWhiteSpace(entity.DefaultCollation) ? null : entity.DefaultCollation,
            string.IsNullOrWhiteSpace(entity.DefaultSchema) ? null : entity.DefaultSchema,
            string.IsNullOrWhiteSpace(entity.SqliteRootPath) ? null : entity.SqliteRootPath,
            entity.MaxDatabaseCount,
            entity.IsDefault,
            entity.IsEnabled,
            entity.TestStatus,
            entity.LastTestAt,
            entity.LastTestMessage,
            summary,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.CreatedBy,
            entity.UpdatedBy);
    }

    private string ResolveSqliteRoot(AiDatabaseHostProfile profile)
    {
        var configured = string.IsNullOrWhiteSpace(profile.SqliteRootPath)
            ? "data/ai-db"
            : profile.SqliteRootPath.Trim();
        ValidateRelativeSqliteRoot(configured);
        return Path.IsPathRooted(configured)
            ? Path.GetFullPath(configured)
            : Path.GetFullPath(Path.Combine(_hostEnvironment.ContentRootPath, configured));
    }

    private static void ValidateProfileSettings(
        string driverCode,
        AiDatabaseProvisionMode provisionMode,
        string? charset,
        string? collation,
        string? schema,
        string? sqliteRootPath)
    {
        if (string.Equals(driverCode, "SQLite", StringComparison.OrdinalIgnoreCase) || provisionMode == AiDatabaseProvisionMode.SQLiteFile)
        {
            ValidateRelativeSqliteRoot(string.IsNullOrWhiteSpace(sqliteRootPath) ? "data/ai-db" : sqliteRootPath.Trim());
        }

        ValidateToken(charset, nameof(charset));
        ValidateToken(collation, nameof(collation));
        ValidateToken(schema, nameof(schema));
    }

    private static void ValidateRelativeSqliteRoot(string path)
    {
        if (Path.IsPathRooted(path) || path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => part == ".."))
        {
            throw new BusinessException("SQLite 托管根目录必须是平台目录下的相对路径。", ErrorCodes.ValidationError);
        }
    }

    private static void ValidateToken(string? value, string name)
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

    private static string ResolveAdminConnection(string driverCode, AiDatabaseHostProfileCreateRequest request)
        => !string.IsNullOrWhiteSpace(request.AdminConnection) ? request.AdminConnection.Trim() : BuildConnection(driverCode, request.Host, request.Port, request.AdminDatabase, request.Username, request.Password);

    private static string ResolveAdminConnection(string driverCode, AiDatabaseHostProfileUpdateRequest request)
        => !string.IsNullOrWhiteSpace(request.AdminConnection) ? request.AdminConnection.Trim() : BuildConnection(driverCode, request.Host, request.Port, request.AdminDatabase, request.Username, request.Password);

    private static bool ShouldBuildConnection(string driverCode, AiDatabaseHostProfileUpdateRequest request)
        => !string.Equals(driverCode, "SQLite", StringComparison.OrdinalIgnoreCase) &&
           !string.IsNullOrWhiteSpace(request.Host) &&
           !string.IsNullOrWhiteSpace(request.Username) &&
           !string.IsNullOrWhiteSpace(request.Password);

    private static string BuildConnection(string driverCode, string? host, int? port, string? database, string? username, string? password)
    {
        if (string.Equals(driverCode, "SQLite", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return string.Empty;
        }

        var builder = new DbConnectionStringBuilder
        {
            ["Server"] = host.Trim(),
            ["User ID"] = username.Trim(),
            ["Password"] = password
        };
        if (port.HasValue)
        {
            builder["Port"] = port.Value;
        }

        if (!string.IsNullOrWhiteSpace(database))
        {
            builder["Database"] = database.Trim();
        }

        if (string.Equals(driverCode, "MySql", StringComparison.OrdinalIgnoreCase))
        {
            builder["Allow User Variables"] = "true";
        }

        return builder.ConnectionString;
    }

    private static string NormalizeDriver(string driverCode)
    {
        var normalized = DataSourceDriverRegistry.NormalizeDriverCode(Required(driverCode, nameof(driverCode)));
        _ = DataSourceDriverRegistry.ResolveDbType(normalized);
        return normalized;
    }

    private static long ParseId(string value, string name)
        => long.TryParse(value, out var id) && id > 0
            ? id
            : throw new BusinessException($"{name} 必须是有效字符串 ID。", ErrorCodes.ValidationError);

    private static string Required(string? value, string name)
        => string.IsNullOrWhiteSpace(value) ? throw new BusinessException($"{name} 不能为空。", ErrorCodes.ValidationError) : value.Trim();
}
