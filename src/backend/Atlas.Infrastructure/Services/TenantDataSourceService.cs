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
            _idGeneratorAccessor.NextId());

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

        entity.Update(request.Name, encryptedConnectionString, request.DbType);
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

            _ = await client.Ado.GetIntAsync("SELECT 1");
            return new TestConnectionResult(true, null);
        }
        catch
        {
            // 等保要求：避免暴露底层连接异常细节
            return new TestConnectionResult(false, "连接失败，请检查数据源配置");
        }
    }

    private static TenantDataSourceDto MapToDto(TenantDataSource entity)
    {
        return new TenantDataSourceDto(
            entity.Id.ToString(),
            entity.TenantIdValue,
            entity.Name,
            entity.DbType,
            entity.IsActive,
            entity.CreatedAt,
            entity.UpdatedAt);
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
