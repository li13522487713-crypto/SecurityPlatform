using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Core.Abstractions;
using Atlas.Domain.Platform.Entities;
using Atlas.Domain.System.Entities;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Microsoft.Extensions.Options;
using SqlSugar;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace Atlas.Infrastructure.Services;

public sealed class TenantDataSourceService : ITenantDataSourceService
{
    private readonly TenantDataSourceRepository _repository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly ITenantDbConnectionFactory _tenantDbConnectionFactory;
    private readonly DatabaseEncryptionOptions _encryptionOptions;
    private readonly ISqlSugarClient _db;

    public TenantDataSourceService(
        TenantDataSourceRepository repository,
        IIdGeneratorAccessor idGeneratorAccessor,
        ITenantDbConnectionFactory tenantDbConnectionFactory,
        IOptions<DatabaseEncryptionOptions> encryptionOptions,
        ISqlSugarClient db)
    {
        _repository = repository;
        _idGeneratorAccessor = idGeneratorAccessor;
        _tenantDbConnectionFactory = tenantDbConnectionFactory;
        _encryptionOptions = encryptionOptions.Value;
        _db = db;
    }

    public async Task<IReadOnlyList<TenantDataSourceDto>> QueryAllAsync(string tenantIdValue, CancellationToken cancellationToken = default)
    {
        var items = await _repository.QueryByTenantAsync(tenantIdValue, cancellationToken);
        return items.Select(MapToDto).ToArray();
    }

    public Task<IReadOnlyList<DataSourceDriverDefinition>> GetDriverDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(DataSourceDriverRegistry.GetDefinitions());
    }

    public async Task<TenantDataSourceDto?> GetByIdAsync(string tenantIdValue, long id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.FindByTenantAndIdAsync(tenantIdValue, id, cancellationToken);
        return entity is null ? null : MapToDto(entity);
    }

    public async Task<long> CreateAsync(
        string tenantIdValue,
        TenantDataSourceCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedDriver = DataSourceDriverRegistry.NormalizeDriverCode(request.DbType);
        var resolvedConnectionString = DataSourceDriverRegistry.ResolveConnectionString(
            normalizedDriver,
            request.Mode,
            request.ConnectionString,
            request.VisualConfig);
        if (string.IsNullOrWhiteSpace(resolvedConnectionString))
        {
            throw new InvalidOperationException("连接字符串不能为空。");
        }

        var encryptedConnectionString = _encryptionOptions.Enabled
            ? TenantDbConnectionFactory.Encrypt(resolvedConnectionString, _encryptionOptions.Key)
            : resolvedConnectionString;
        var ownerAppId = ResolveOwnerAppId(request.OwnershipScope, request.OwnerAppInstanceId, request.AppId);

        var entity = new TenantDataSource(
            tenantIdValue,
            request.Name,
            encryptedConnectionString,
            normalizedDriver,
            _idGeneratorAccessor.NextId(),
            ownerAppId,
            NormalizeMaxPoolSize(request.MaxPoolSize),
            NormalizeConnectionTimeoutSeconds(request.ConnectionTimeoutSeconds));

        await _repository.AddAsync(entity, cancellationToken);
        await InvalidateDataSourceRelatedCachesAsync(tenantIdValue, entity.Id, entity.AppId, cancellationToken);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(
        string tenantIdValue,
        long id,
        TenantDataSourceUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _repository.FindByTenantAndIdAsync(tenantIdValue, id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        var normalizedDriver = DataSourceDriverRegistry.NormalizeDriverCode(request.DbType);
        var encryptedConnectionString = entity.EncryptedConnectionString;
        var ownerAppId = ResolveOwnerAppIdForUpdate(request.OwnershipScope, request.OwnerAppInstanceId, request.AppId, entity.AppId);
        var resolvedConnectionString = DataSourceDriverRegistry.ResolveConnectionString(
            normalizedDriver,
            request.Mode,
            request.ConnectionString,
            request.VisualConfig);
        if (!string.IsNullOrWhiteSpace(resolvedConnectionString))
        {
            encryptedConnectionString = _encryptionOptions.Enabled
                ? TenantDbConnectionFactory.Encrypt(resolvedConnectionString, _encryptionOptions.Key)
                : resolvedConnectionString;
        }

        entity.Update(
            request.Name,
            encryptedConnectionString,
            normalizedDriver,
            ownerAppId,
            NormalizeMaxPoolSize(request.MaxPoolSize),
            NormalizeConnectionTimeoutSeconds(request.ConnectionTimeoutSeconds));
        await _repository.UpdateAsync(entity, cancellationToken);
        await InvalidateDataSourceRelatedCachesAsync(entity.TenantIdValue, entity.Id, entity.AppId, cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(string tenantIdValue, long id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.FindByTenantAndIdAsync(tenantIdValue, id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        await _repository.DeleteByTenantAndIdAsync(tenantIdValue, id, cancellationToken);
        await InvalidateDataSourceRelatedCachesAsync(entity.TenantIdValue, entity.Id, entity.AppId, cancellationToken);
        return true;
    }

    public async Task<TestConnectionResult> TestConnectionAsync(TestConnectionRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var normalizedDriver = DataSourceDriverRegistry.NormalizeDriverCode(request.DbType);
            var resolvedConnectionString = DataSourceDriverRegistry.ResolveConnectionString(
                normalizedDriver,
                request.Mode,
                request.ConnectionString,
                request.VisualConfig);
            if (string.IsNullOrWhiteSpace(resolvedConnectionString))
            {
                throw new InvalidOperationException("连接字符串不能为空。");
            }

            using var client = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = resolvedConnectionString,
                DbType = DataSourceDriverRegistry.ResolveDbType(normalizedDriver),
                IsAutoCloseConnection = true
            });

            cancellationToken.ThrowIfCancellationRequested();
            _ = client.DbMaintenance.GetTableInfoList(false);
            stopwatch.Stop();
            return new TestConnectionResult(true, null, (int)stopwatch.ElapsedMilliseconds);
        }
        catch
        {
            // 等保要求：避免暴露底层连接异常细节
            stopwatch.Stop();
            return new TestConnectionResult(false, "连接失败，请检查数据源配置", (int)stopwatch.ElapsedMilliseconds);
        }
    }

    public async Task<TestConnectionResult> TestConnectionByDataSourceIdAsync(string tenantId, long id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.FindByTenantAndIdAsync(tenantId, id, cancellationToken);
        if (entity is null)
        {
            return new TestConnectionResult(false, "TenantDataSourceNotFound");
        }

        var connectionString = _encryptionOptions.Enabled
            ? TenantDbConnectionFactory.Decrypt(entity.EncryptedConnectionString, _encryptionOptions.Key)
            : entity.EncryptedConnectionString;

        var result = await TestConnectionAsync(new TestConnectionRequest(connectionString, entity.DbType), cancellationToken);
        entity.MarkTestResult(result.Success, result.ErrorMessage, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, cancellationToken);
        return result;
    }

    private TenantDataSourceDto MapToDto(TenantDataSource entity)
    {
        var connectionString = _encryptionOptions.Enabled
            ? TenantDbConnectionFactory.Decrypt(entity.EncryptedConnectionString, _encryptionOptions.Key)
            : entity.EncryptedConnectionString;
        var safeInfo = ParseSafeConnectionInfo(entity.DbType, connectionString);
        return new TenantDataSourceDto(
            entity.Id.ToString(),
            entity.TenantIdValue,
            entity.Name,
            entity.DbType,
            entity.DbType,
            safeInfo.Host,
            safeInfo.Port,
            safeInfo.DatabaseName,
            safeInfo.MaskedConnectionSummary,
            entity.AppId.HasValue ? TenantDataSourceOwnershipScopes.AppScoped : TenantDataSourceOwnershipScopes.Platform,
            entity.AppId?.ToString(),
            entity.AppId?.ToString(),
            entity.MaxPoolSize,
            entity.ConnectionTimeoutSeconds,
            entity.LastTestSuccess,
            entity.LastTestedAt,
            entity.LastTestMessage,
            entity.IsActive,
            entity.CreatedAt,
            entity.UpdatedAt);
    }

    private static SafeConnectionInfo ParseSafeConnectionInfo(string driverCode, string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return SafeConnectionInfo.Empty;
        }

        if (string.Equals(driverCode, "SQLite", StringComparison.OrdinalIgnoreCase)
            || string.Equals(driverCode, "Access", StringComparison.OrdinalIgnoreCase))
        {
            var parts = ParseConnectionString(connectionString);
            var dataSource = GetFirst(parts, "Data Source", "DataSource", "Filename", "File Name");
            var sqliteFileName = string.IsNullOrWhiteSpace(dataSource)
                ? null
                : Path.GetFileName(dataSource.Replace("\"", string.Empty));
            return new SafeConnectionInfo(
                Host: null,
                Port: null,
                DatabaseName: sqliteFileName,
                MaskedConnectionSummary: sqliteFileName is null ? driverCode : $"{driverCode} / {sqliteFileName}");
        }

        var tokens = ParseConnectionString(connectionString);
        var host = GetFirst(tokens, "Server", "Host", "Data Source", "Address", "Addr", "Network Address", "Hostname");
        host = NormalizeServerHost(host);
        var portValue = GetFirst(tokens, "Port");
        var databaseName = GetFirst(tokens, "Database", "Initial Catalog", "Service Name", "SID");
        int? port = null;
        if (int.TryParse(portValue, out var parsedPort) && parsedPort > 0)
        {
            port = parsedPort;
        }

        var summary = BuildSafeSummary(driverCode, host, port, databaseName);
        return new SafeConnectionInfo(host, port, databaseName, summary);
    }

    private static Dictionary<string, string> ParseConnectionString(string connectionString)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var segment in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var idx = segment.IndexOf('=');
            if (idx <= 0 || idx >= segment.Length - 1)
            {
                continue;
            }

            var key = segment[..idx].Trim();
            var value = segment[(idx + 1)..].Trim().Trim('"');
            if (key.Length == 0)
            {
                continue;
            }

            result[key] = value;
        }

        return result;
    }

    private static string? GetFirst(IReadOnlyDictionary<string, string> values, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? NormalizeServerHost(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var value = raw.Trim();
        if (value.Contains('/') && !value.Contains('='))
        {
            value = value.Split('/', 2, StringSplitOptions.TrimEntries)[0];
        }

        if (value.Contains(','))
        {
            value = value.Split(',', 2, StringSplitOptions.TrimEntries)[0];
        }

        if (value.Contains(':') && !Regex.IsMatch(value, @"^\[[^\]]+\](:\d+)?$"))
        {
            value = value.Split(':', 2, StringSplitOptions.TrimEntries)[0];
        }

        return value;
    }

    private static string BuildSafeSummary(string driverCode, string? host, int? port, string? databaseName)
    {
        var segments = new List<string> { driverCode };
        if (!string.IsNullOrWhiteSpace(host))
        {
            segments.Add(port.HasValue ? $"{host}:{port}" : host);
        }

        if (!string.IsNullOrWhiteSpace(databaseName))
        {
            segments.Add(databaseName);
        }

        return string.Join(" / ", segments);
    }

    private sealed record SafeConnectionInfo(string? Host, int? Port, string? DatabaseName, string? MaskedConnectionSummary)
    {
        public static readonly SafeConnectionInfo Empty = new(null, null, null, null);
    }

    private static long? ParseAppId(string? appId)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return null;
        }

        return long.TryParse(appId, out var parsed) && parsed > 0
            ? parsed
            : throw new InvalidOperationException("AppId 格式无效");
    }

    private static long? ResolveOwnerAppId(string? ownershipScope, string? ownerAppInstanceId, string? appId)
    {
        var hasLegacyAppId = !string.IsNullOrWhiteSpace(appId);
        var hasOwnerAppInstanceId = !string.IsNullOrWhiteSpace(ownerAppInstanceId);

        if (hasLegacyAppId && hasOwnerAppInstanceId)
        {
            throw new InvalidOperationException("appId 与 ownerAppInstanceId 不能同时提供。");
        }

        if (hasOwnerAppInstanceId)
        {
            return ParseAppId(ownerAppInstanceId);
        }

        if (hasLegacyAppId)
        {
            return ParseAppId(appId);
        }

        var normalizedScope = NormalizeOwnershipScope(ownershipScope);
        return string.Equals(normalizedScope, TenantDataSourceOwnershipScopes.Platform, StringComparison.OrdinalIgnoreCase)
            ? null
            : throw new InvalidOperationException("应用级数据源必须提供 ownerAppInstanceId。");
    }

    private static long? ResolveOwnerAppIdForUpdate(
        string? ownershipScope,
        string? ownerAppInstanceId,
        string? appId,
        long? currentAppId)
    {
        if (string.IsNullOrWhiteSpace(ownershipScope)
            && string.IsNullOrWhiteSpace(ownerAppInstanceId)
            && string.IsNullOrWhiteSpace(appId))
        {
            return currentAppId;
        }

        return ResolveOwnerAppId(ownershipScope, ownerAppInstanceId, appId);
    }

    private static string NormalizeOwnershipScope(string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return TenantDataSourceOwnershipScopes.Platform;
        }

        if (string.Equals(scope, TenantDataSourceOwnershipScopes.Platform, StringComparison.OrdinalIgnoreCase))
        {
            return TenantDataSourceOwnershipScopes.Platform;
        }

        if (string.Equals(scope, TenantDataSourceOwnershipScopes.AppScoped, StringComparison.OrdinalIgnoreCase))
        {
            return TenantDataSourceOwnershipScopes.AppScoped;
        }

        throw new InvalidOperationException("ownershipScope 仅支持 Platform 或 AppScoped。");
    }

    private static int NormalizeMaxPoolSize(int value)
    {
        if (value < 1)
        {
            return 50;
        }

        return value > 500 ? 500 : value;
    }

    private static int NormalizeConnectionTimeoutSeconds(int value)
    {
        if (value < 1)
        {
            return 15;
        }

        return value > 120 ? 120 : value;
    }

    private async Task InvalidateDataSourceRelatedCachesAsync(
        string tenantIdValue,
        long dataSourceId,
        long? appId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(tenantIdValue, out var tenantGuid))
        {
            _tenantDbConnectionFactory.InvalidateCache(tenantIdValue);
            return;
        }

        var boundAppIds = await _db.Queryable<TenantAppDataSourceBinding>()
            .Where(item =>
                item.TenantIdValue == tenantGuid
                && item.DataSourceId == dataSourceId)
            .Select(item => item.TenantAppInstanceId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var affectedAppIds = new HashSet<long>(boundAppIds);
        if (appId.HasValue && appId.Value > 0)
        {
            affectedAppIds.Add(appId.Value);
        }

        if (affectedAppIds.Count == 0)
        {
            _tenantDbConnectionFactory.InvalidateCache(tenantIdValue);
            return;
        }

        foreach (var affectedAppId in affectedAppIds)
        {
            _tenantDbConnectionFactory.InvalidateCache(tenantIdValue, affectedAppId);
        }
    }

    public async Task<IReadOnlyList<DataSourceConsumerItem>> GetConsumersAsync(
        string tenantIdValue,
        long dataSourceId,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(tenantIdValue, out var tenantGuid))
        {
            return Array.Empty<DataSourceConsumerItem>();
        }

        var bindings = await _db.Queryable<TenantAppDataSourceBinding>()
            .Where(b => b.TenantIdValue == tenantGuid && b.DataSourceId == dataSourceId)
            .ToListAsync(cancellationToken);
        if (bindings.Count == 0)
        {
            return Array.Empty<DataSourceConsumerItem>();
        }

        var instanceIds = bindings.Select(b => b.TenantAppInstanceId).Distinct().ToArray();
        var apps = await _db.Queryable<AppManifest>()
            .Where(a => a.TenantIdValue == tenantGuid && SqlFunc.ContainsArray(instanceIds, a.Id))
            .ToListAsync(cancellationToken);
        var appNameMap = apps.ToDictionary(a => a.Id, a => a.Name);

        return bindings.Select(b => new DataSourceConsumerItem(
            b.Id.ToString(),
            b.TenantAppInstanceId.ToString(),
            appNameMap.TryGetValue(b.TenantAppInstanceId, out var name) ? name : b.TenantAppInstanceId.ToString(),
            b.BindingType.ToString(),
            b.IsActive,
            b.BoundAt)).ToArray();
    }

    public async Task<IReadOnlyList<DataSourceOrphanItem>> GetOrphansAsync(
        string tenantIdValue,
        CancellationToken cancellationToken = default)
    {
        var allDataSources = await _repository.QueryByTenantAsync(tenantIdValue, cancellationToken);
        if (allDataSources.Count == 0)
        {
            return Array.Empty<DataSourceOrphanItem>();
        }

        var allIds = allDataSources.Select(ds => ds.Id).ToArray();

        if (!Guid.TryParse(tenantIdValue, out var tenantGuid))
        {
            return allDataSources.Select(ds => new DataSourceOrphanItem(
                ds.Id.ToString(), ds.Name, ds.DbType, ds.IsActive, ds.CreatedAt, ds.LastTestedAt)).ToArray();
        }

        var boundIds = await _db.Queryable<TenantAppDataSourceBinding>()
            .Where(b => b.TenantIdValue == tenantGuid && b.IsActive && SqlFunc.ContainsArray(allIds, b.DataSourceId))
            .Select(b => b.DataSourceId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var appRefIds = await _db.Queryable<AppManifest>()
            .Where(a => a.TenantIdValue == tenantGuid && a.DataSourceId != null && SqlFunc.ContainsArray(allIds, a.DataSourceId!.Value))
            .Select(a => a.DataSourceId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var usedIds = new HashSet<long>(boundIds.Concat(appRefIds));
        return allDataSources
            .Where(ds => !usedIds.Contains(ds.Id))
            .Select(ds => new DataSourceOrphanItem(
                ds.Id.ToString(),
                ds.Name,
                ds.DbType,
                ds.IsActive,
                ds.CreatedAt,
                ds.LastTestedAt))
            .ToArray();
    }
}
