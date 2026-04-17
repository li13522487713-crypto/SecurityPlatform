using Atlas.Application.System.Abstractions;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.System.Entities;
using Atlas.Infrastructure.Options;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

public sealed class AppDataSourceProvisioner : IAppDataSourceProvisioner
{
    private readonly ISqlSugarClient _mainDb;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly ITenantDbConnectionFactory _tenantDbConnectionFactory;
    private readonly DatabaseEncryptionOptions _databaseEncryptionOptions;
    private readonly ILogger<AppDataSourceProvisioner> _logger;

    public AppDataSourceProvisioner(
        ISqlSugarClient mainDb,
        IIdGeneratorAccessor idGeneratorAccessor,
        ITenantDbConnectionFactory tenantDbConnectionFactory,
        IOptions<DatabaseEncryptionOptions> databaseEncryptionOptions,
        ILogger<AppDataSourceProvisioner> logger)
    {
        _mainDb = mainDb;
        _idGeneratorAccessor = idGeneratorAccessor;
        _tenantDbConnectionFactory = tenantDbConnectionFactory;
        _databaseEncryptionOptions = databaseEncryptionOptions.Value;
        _logger = logger;
    }

    public async Task EnsureProvisionedAsync(
        TenantId tenantId,
        long appInstanceId,
        string appKey,
        long operatorUserId,
        long? preferredDataSourceId = null,
        CancellationToken cancellationToken = default)
    {
        if (appInstanceId <= 0)
        {
            throw new BusinessException(ErrorCodes.ValidationError, $"应用实例ID无效。AppInstanceId={appInstanceId}");
        }

        if (string.IsNullOrWhiteSpace(appKey))
        {
            throw new BusinessException(ErrorCodes.ValidationError, $"应用标识无效。AppInstanceId={appInstanceId}");
        }

        var tenantIdText = tenantId.Value.ToString("D");
        var now = DateTimeOffset.UtcNow;
        var dataSourceId = preferredDataSourceId.GetValueOrDefault();
        if (dataSourceId <= 0)
        {
            dataSourceId = await ResolvePrimaryDataSourceIdAsync(tenantId, appInstanceId, cancellationToken);
        }

        if (dataSourceId <= 0)
        {
            var connectionString = BuildAppSQLiteConnectionString(appKey, appInstanceId);
            var encryptedConnectionString = _databaseEncryptionOptions.Enabled
                ? TenantDbConnectionFactory.Encrypt(connectionString, _databaseEncryptionOptions.Key)
                : connectionString;
            var entity = new TenantDataSource(
                tenantIdText,
                $"App-{appKey}-{appInstanceId}",
                encryptedConnectionString,
                "SQLite",
                _idGeneratorAccessor.NextId(),
                appInstanceId);
            await _mainDb.Insertable(entity).ExecuteCommandAsync(cancellationToken);
            dataSourceId = entity.Id;
        }

        await UpsertPrimaryBindingAsync(tenantId, appInstanceId, dataSourceId, operatorUserId, now, cancellationToken);
        await UpsertRoutePolicyAsync(tenantId, appInstanceId, operatorUserId, now, cancellationToken);
        _tenantDbConnectionFactory.InvalidateCache(tenantIdText, appInstanceId);

        _logger.LogInformation(
            "应用数据源供给完成。TenantId={TenantId}; AppInstanceId={AppInstanceId}; DataSourceId={DataSourceId}",
            tenantId.Value,
            appInstanceId,
            dataSourceId);

        await Task.CompletedTask;
    }

    private async Task<long> ResolvePrimaryDataSourceIdAsync(
        TenantId tenantId,
        long appInstanceId,
        CancellationToken cancellationToken)
    {
        var binding = await _mainDb.Queryable<TenantAppDataSourceBinding>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value
                && x.TenantAppInstanceId == appInstanceId
                && x.IsActive
                && x.BindingType == TenantAppDataSourceBindingType.Primary
                && x.DataSourceId > 0)
            .OrderByDescending(x => x.UpdatedAt ?? x.BoundAt)
            .FirstAsync(cancellationToken);
        if (binding is null)
        {
            return 0;
        }

        var tenantIdText = tenantId.Value.ToString("D");
        var exists = await _mainDb.Queryable<TenantDataSource>()
            .AnyAsync(x =>
                x.TenantIdValue == tenantIdText
                && x.Id == binding.DataSourceId
                && x.IsActive,
                cancellationToken);
        return exists ? binding.DataSourceId : 0;
    }

    private async Task UpsertPrimaryBindingAsync(
        TenantId tenantId,
        long appInstanceId,
        long dataSourceId,
        long operatorUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var binding = await _mainDb.Queryable<TenantAppDataSourceBinding>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value
                && x.TenantAppInstanceId == appInstanceId
                && x.BindingType == TenantAppDataSourceBindingType.Primary)
            .FirstAsync(cancellationToken);
        if (binding is null)
        {
            var entity = new TenantAppDataSourceBinding(
                tenantId,
                appInstanceId,
                dataSourceId,
                TenantAppDataSourceBindingType.Primary,
                operatorUserId,
                _idGeneratorAccessor.NextId(),
                now,
                "应用创建时自动供给");
            await _mainDb.Insertable(entity).ExecuteCommandAsync(cancellationToken);
            return;
        }

        binding.Rebind(
            dataSourceId,
            TenantAppDataSourceBindingType.Primary,
            operatorUserId,
            now,
            "应用创建时自动供给");
        await _mainDb.Updateable(binding)
            .Where(x => x.Id == binding.Id && x.TenantIdValue == tenantId.Value)
            .ExecuteCommandAsync(cancellationToken);
    }

    private async Task UpsertRoutePolicyAsync(
        TenantId tenantId,
        long appInstanceId,
        long operatorUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var policy = await _mainDb.Queryable<AppDataRoutePolicy>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppInstanceId == appInstanceId)
            .FirstAsync(cancellationToken);
        if (policy is null)
        {
            policy = new AppDataRoutePolicy(
                tenantId,
                appInstanceId,
                "AppOnly",
                readOnlyWindow: false,
                dualWriteEnabled: false,
                operatorUserId,
                _idGeneratorAccessor.NextId(),
                now);
            await _mainDb.Insertable(policy).ExecuteCommandAsync(cancellationToken);
            return;
        }

        policy.SetMode("AppOnly", readOnlyWindow: false, dualWriteEnabled: false, operatorUserId, now);
        await _mainDb.Updateable(policy)
            .Where(x => x.Id == policy.Id && x.TenantIdValue == tenantId.Value)
            .ExecuteCommandAsync(cancellationToken);
    }

    private string BuildAppSQLiteConnectionString(string appKey, long appInstanceId)
    {
        var mainConnectionString = _mainDb.CurrentConnectionConfig.ConnectionString;
        var builder = new SqliteConnectionStringBuilder(mainConnectionString);
        var mainDbPath = builder.DataSource;
        var mainDirectory = string.IsNullOrWhiteSpace(mainDbPath)
            ? AppContext.BaseDirectory
            : Path.GetDirectoryName(Path.GetFullPath(mainDbPath)) ?? AppContext.BaseDirectory;
        var safeAppKey = SanitizeFileNameSegment(appKey);
        var dbFileName = $"atlas.app.{safeAppKey}.{appInstanceId}.db";
        var appDbPath = Path.Combine(mainDirectory, dbFileName);
        Directory.CreateDirectory(mainDirectory);
        return new SqliteConnectionStringBuilder
        {
            DataSource = appDbPath
        }.ToString();
    }

    private static string SanitizeFileNameSegment(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = value.Trim()
            .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
            .ToArray();
        var sanitized = new string(chars);
        return string.IsNullOrWhiteSpace(sanitized) ? "app" : sanitized;
    }
}
