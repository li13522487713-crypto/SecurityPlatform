using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Core.Abstractions;
using Atlas.Domain.Platform.Entities;
using Atlas.Domain.System.Entities;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Microsoft.Extensions.Options;
using SqlSugar;
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

        var entity = new TenantDataSource(
            tenantIdValue,
            request.Name,
            encryptedConnectionString,
            normalizedDriver,
            _idGeneratorAccessor.NextId(),
            ParseAppId(request.AppId),
            NormalizeMaxPoolSize(request.MaxPoolSize),
            NormalizeConnectionTimeoutSeconds(request.ConnectionTimeoutSeconds));

        await _repository.AddAsync(entity, cancellationToken);
        _tenantDbConnectionFactory.InvalidateCache(tenantIdValue, entity.AppId);
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
            NormalizeMaxPoolSize(request.MaxPoolSize),
            NormalizeConnectionTimeoutSeconds(request.ConnectionTimeoutSeconds));
        await _repository.UpdateAsync(entity, cancellationToken);
        _tenantDbConnectionFactory.InvalidateCache(entity.TenantIdValue, entity.AppId);
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
        _tenantDbConnectionFactory.InvalidateCache(entity.TenantIdValue, entity.AppId);
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

    private static TenantDataSourceDto MapToDto(TenantDataSource entity)
    {
        return new TenantDataSourceDto(
            entity.Id.ToString(),
            entity.TenantIdValue,
            entity.Name,
            entity.DbType,
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