using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Core.Abstractions;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Atlas.Domain.System.Entities;
using Microsoft.Extensions.Options;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

public sealed class TenantDataSourceService : ITenantDataSourceService
{
    private readonly TenantDataSourceRepository _repository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly ITenantDbConnectionFactory _tenantDbConnectionFactory;
    private readonly DatabaseEncryptionOptions _encryptionOptions;

    public TenantDataSourceService(
        TenantDataSourceRepository repository,
        IIdGeneratorAccessor idGeneratorAccessor,
        ITenantDbConnectionFactory tenantDbConnectionFactory,
        IOptions<DatabaseEncryptionOptions> encryptionOptions)
    {
        _repository = repository;
        _idGeneratorAccessor = idGeneratorAccessor;
        _tenantDbConnectionFactory = tenantDbConnectionFactory;
        _encryptionOptions = encryptionOptions.Value;
    }

    public async Task<IReadOnlyList<TenantDataSourceDto>> QueryAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await _repository.QueryAllAsync(cancellationToken);
        return items.Select(MapToDto).ToArray();
    }

    public async Task<TenantDataSourceDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.FindByIdAsync(id, cancellationToken);
        return entity is null ? null : MapToDto(entity);
    }

    public async Task<long> CreateAsync(TenantDataSourceCreateRequest request, CancellationToken cancellationToken = default)
    {
        var encryptedConnectionString = _encryptionOptions.Enabled
            ? TenantDbConnectionFactory.Encrypt(request.ConnectionString, _encryptionOptions.Key)
            : request.ConnectionString;

        var entity = new TenantDataSource(
            request.TenantIdValue,
            request.Name,
            encryptedConnectionString,
            request.DbType,
            _idGeneratorAccessor.NextId(),
            ParseAppId(request.AppId),
            NormalizeMaxPoolSize(request.MaxPoolSize),
            NormalizeConnectionTimeoutSeconds(request.ConnectionTimeoutSeconds));

        await _repository.AddAsync(entity, cancellationToken);
        _tenantDbConnectionFactory.InvalidateCache(request.TenantIdValue);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(long id, TenantDataSourceUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.FindByIdAsync(id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        var encryptedConnectionString = _encryptionOptions.Enabled
            ? TenantDbConnectionFactory.Encrypt(request.ConnectionString, _encryptionOptions.Key)
            : request.ConnectionString;

        entity.Update(
            request.Name,
            encryptedConnectionString,
            request.DbType,
            NormalizeMaxPoolSize(request.MaxPoolSize),
            NormalizeConnectionTimeoutSeconds(request.ConnectionTimeoutSeconds));
        await _repository.UpdateAsync(entity, cancellationToken);
        _tenantDbConnectionFactory.InvalidateCache(entity.TenantIdValue);
        return true;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.FindByIdAsync(id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        await _repository.DeleteAsync(id, cancellationToken);
        _tenantDbConnectionFactory.InvalidateCache(entity.TenantIdValue);
        return true;
    }

    public async Task<TestConnectionResult> TestConnectionAsync(TestConnectionRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = request.ConnectionString,
                DbType = ParseDbType(request.DbType),
                IsAutoCloseConnection = true
            });

            cancellationToken.ThrowIfCancellationRequested();
            _ = client.DbMaintenance.GetTableInfoList(false);
            return new TestConnectionResult(true, null);
        }
        catch
        {
            // 等保要求：避免暴露底层连接异常细节
            return new TestConnectionResult(false, "连接失败，请检查数据源配置");
        }
    }

    public async Task<TestConnectionResult> TestConnectionByDataSourceIdAsync(string tenantId, long id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.FindByTenantAndIdAsync(tenantId, id, cancellationToken);
        if (entity is null)
        {
            return new TestConnectionResult(false, "数据源不存在");
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

    private static DbType ParseDbType(string dbType)
    {
        return dbType.ToLowerInvariant() switch
        {
            "sqlite" => DbType.Sqlite,
            "sqlserver" => DbType.SqlServer,
            "mysql" => DbType.MySql,
            "postgresql" => DbType.PostgreSQL,
            _ => DbType.Sqlite
        };
    }
}
