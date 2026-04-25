using System.Collections.Concurrent;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services.AiPlatform;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlSugar;

namespace Atlas.Infrastructure.Services.DatabaseStructure;

public interface IAiDatabaseClientFactory
{
    Task<SqlSugarClient> GetClientAsync(TenantId tenantId, long databaseId, AiDatabaseRecordEnvironment environment, CancellationToken cancellationToken);

    Task<(AiDatabase Database, SqlSugarClient Client)> CreateClientAsync(
        TenantId tenantId,
        long databaseId,
        AiDatabaseRecordEnvironment environment,
        CancellationToken cancellationToken);

    void RemoveFromCache(long databaseId, AiDatabaseRecordEnvironment environment);

    Task<bool> TestConnectionAsync(TenantId tenantId, long databaseId, AiDatabaseRecordEnvironment environment, CancellationToken cancellationToken);
}

public sealed class AiDatabaseClientFactory : IAiDatabaseClientFactory, IDisposable
{
    private readonly ConcurrentDictionary<string, SqlSugarClient> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly AiDatabaseRepository _repository;
    private readonly IAiDatabaseProvisioner _provisioner;
    private readonly DatabaseEncryptionOptions _encryptionOptions;
    private readonly AiDatabaseHostingOptions _hostingOptions;
    private readonly ILogger<AiDatabaseClientFactory> _logger;

    public AiDatabaseClientFactory(
        AiDatabaseRepository repository,
        IAiDatabaseProvisioner provisioner,
        IOptions<DatabaseEncryptionOptions> encryptionOptions,
        IOptions<AiDatabaseHostingOptions> hostingOptions,
        ILogger<AiDatabaseClientFactory> logger)
    {
        _repository = repository;
        _provisioner = provisioner;
        _encryptionOptions = encryptionOptions.Value;
        _hostingOptions = hostingOptions.Value;
        _logger = logger;
    }

    public async Task<SqlSugarClient> GetClientAsync(
        TenantId tenantId,
        long databaseId,
        AiDatabaseRecordEnvironment environment,
        CancellationToken cancellationToken)
    {
        var (database, client) = await CreateClientAsync(tenantId, databaseId, environment, cancellationToken);
        return client;
    }

    public async Task<(AiDatabase Database, SqlSugarClient Client)> CreateClientAsync(
        TenantId tenantId,
        long databaseId,
        AiDatabaseRecordEnvironment environment,
        CancellationToken cancellationToken)
    {
        var database = await _repository.FindByIdAsync(tenantId, databaseId, cancellationToken)
            ?? throw new BusinessException("数据库不存在。", ErrorCodes.NotFound);

        await _provisioner.EnsureProvisionedAsync(database, cancellationToken);

        var cacheKey = BuildCacheKey(tenantId, databaseId, environment);
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            return (database, cached);
        }

        var encrypted = environment == AiDatabaseRecordEnvironment.Online
            ? database.EncryptedOnlineConnection
            : database.EncryptedDraftConnection;

        if (string.IsNullOrWhiteSpace(encrypted))
        {
            throw new BusinessException("数据库连接尚未初始化。", ErrorCodes.ValidationError);
        }

        var connectionString = _encryptionOptions.Enabled
            ? TenantDbConnectionFactory.Decrypt(encrypted, _encryptionOptions.Key)
            : encrypted;

        try
        {
            var client = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = connectionString,
                DbType = DataSourceDriverRegistry.ResolveDbType(database.DriverCode),
                IsAutoCloseConnection = true
            });
            client.Ado.CommandTimeOut = Math.Clamp(_hostingOptions.CommandTimeoutSeconds, 1, 60);
            _cache[cacheKey] = client;
            return (database, client);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to create AI database client db={DatabaseId} env={Environment} conn={ConnectionString}",
                databaseId,
                environment,
                ConnectionStringMasker.Mask(connectionString));
            throw new BusinessException("DATABASE_CONNECTION_FAILED", "数据库连接创建失败。");
        }
    }

    public void RemoveFromCache(long databaseId, AiDatabaseRecordEnvironment environment)
    {
        var suffix = $":{databaseId}:{environment}";
        foreach (var key in _cache.Keys.Where(key => key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)).ToList())
        {
            if (_cache.TryRemove(key, out var client))
            {
                client.Dispose();
            }
        }
    }

    public async Task<bool> TestConnectionAsync(
        TenantId tenantId,
        long databaseId,
        AiDatabaseRecordEnvironment environment,
        CancellationToken cancellationToken)
    {
        var client = await GetClientAsync(tenantId, databaseId, environment, cancellationToken);
        try
        {
            await client.Ado.GetScalarAsync("SELECT 1");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI database connection test failed db={DatabaseId} env={Environment}", databaseId, environment);
            RemoveFromCache(databaseId, environment);
            return false;
        }
    }

    public void Dispose()
    {
        foreach (var client in _cache.Values)
        {
            client.Dispose();
        }

        _cache.Clear();
    }

    private static string BuildCacheKey(TenantId tenantId, long databaseId, AiDatabaseRecordEnvironment environment)
        => $"{tenantId.Value:N}:{databaseId}:{environment}";
}
