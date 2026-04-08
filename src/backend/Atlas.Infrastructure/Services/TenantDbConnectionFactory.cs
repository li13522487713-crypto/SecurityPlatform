using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Infrastructure.Caching;
using Atlas.Infrastructure.Observability;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 租户数据库连接工厂实现（等保2.0 数据隔离）
/// <para>从数据库读取租户自定义数据源，连接字符串使用 AES-256 加密存储。</para>
/// </summary>
public sealed class TenantDbConnectionFactory : ITenantDbConnectionFactory, IAppDbConnectionResolver
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    private readonly TenantDataSourceRepository _repository;
    private readonly IAtlasHybridCache _cache;
    private readonly DatabaseEncryptionOptions _encryptionOptions;

    public TenantDbConnectionFactory(
        TenantDataSourceRepository repository,
        IAtlasHybridCache cache,
        IOptions<DatabaseEncryptionOptions> encryptionOptions)
    {
        _repository = repository;
        _cache = cache;
        _encryptionOptions = encryptionOptions.Value;
    }

    public async Task<string?> GetConnectionStringAsync(string tenantId, CancellationToken ct = default)
    {
        var info = await GetConnectionInfoAsync(tenantId, ct);
        return info?.ConnectionString;
    }

    public async Task<TenantDbConnectionInfo?> GetConnectionInfoAsync(string tenantId, CancellationToken ct = default)
    {
        var cacheKey = AtlasCacheKeys.TenantConnection.TenantInfo(tenantId);
        var cached = await _cache.TryGetAsync<TenantDbConnectionInfo?>(cacheKey, cancellationToken: ct);
        if (cached.Found)
        {
            AtlasMetrics.RecordTenantDatasourceResolve(0, "success", "cache");
            return cached.Value;
        }

        var stopwatch = Stopwatch.StartNew();
        var status = "success";
        var sourceTag = "database";
        try
        {
            var source = await _repository.FindByTenantIdAsync(tenantId, ct);
            if (source is null)
            {
                sourceTag = "not_found";
                await _cache.SetAsync<TenantDbConnectionInfo?>(
                    cacheKey,
                    value: null,
                    CacheDuration,
                    [AtlasCacheTags.TenantConnectionTenant(tenantId)],
                    cancellationToken: ct);
                return null;
            }

            var connectionString = _encryptionOptions.Enabled
                ? Decrypt(source.EncryptedConnectionString, _encryptionOptions.Key)
                : source.EncryptedConnectionString;

            var info = new TenantDbConnectionInfo(connectionString, source.DbType);
            await _cache.SetAsync(
                cacheKey,
                info,
                CacheDuration,
                [AtlasCacheTags.TenantConnectionTenant(tenantId)],
                cancellationToken: ct);
            return info;
        }
        catch
        {
            status = "failed";
            throw;
        }
        finally
        {
            AtlasMetrics.RecordTenantDatasourceResolve(stopwatch.Elapsed.TotalMilliseconds, status, sourceTag);
        }
    }

    public async Task<TenantDbConnectionInfo?> GetConnectionInfoAsync(
        string tenantId,
        long tenantAppInstanceId,
        CancellationToken ct = default)
    {
        var cacheKey = AtlasCacheKeys.TenantConnection.AppInfo(tenantId, tenantAppInstanceId);
        var cached = await _cache.TryGetAsync<TenantDbConnectionInfo?>(cacheKey, cancellationToken: ct);
        if (cached.Found)
        {
            AtlasMetrics.RecordTenantDatasourceResolve(0, "success", "cache");
            return cached.Value;
        }

        var stopwatch = Stopwatch.StartNew();
        var status = "success";
        var sourceTag = "database";
        try
        {
            if (!Guid.TryParse(tenantId, out var tenantGuid))
            {
                sourceTag = "invalid_tenant";
                await _cache.SetAsync<TenantDbConnectionInfo?>(
                    cacheKey,
                    value: null,
                    CacheDuration,
                    [AtlasCacheTags.TenantConnectionTenant(tenantId), AtlasCacheTags.TenantConnectionApp(tenantId, tenantAppInstanceId)],
                    cancellationToken: ct);
                return null;
            }

            var source = await _repository.FindByTenantAndAppInstanceBindingAsync(tenantGuid, tenantAppInstanceId, ct);
            if (source is null)
            {
                sourceTag = "not_found";
                await _cache.SetAsync<TenantDbConnectionInfo?>(
                    cacheKey,
                    value: null,
                    CacheDuration,
                    [AtlasCacheTags.TenantConnectionTenant(tenantId), AtlasCacheTags.TenantConnectionApp(tenantId, tenantAppInstanceId)],
                    cancellationToken: ct);
                return null;
            }

            var connectionString = _encryptionOptions.Enabled
                ? Decrypt(source.EncryptedConnectionString, _encryptionOptions.Key)
                : source.EncryptedConnectionString;

            var info = new TenantDbConnectionInfo(connectionString, source.DbType);
            await _cache.SetAsync(
                cacheKey,
                info,
                CacheDuration,
                [AtlasCacheTags.TenantConnectionTenant(tenantId), AtlasCacheTags.TenantConnectionApp(tenantId, tenantAppInstanceId)],
                cancellationToken: ct);
            return info;
        }
        catch
        {
            status = "failed";
            throw;
        }
        finally
        {
            AtlasMetrics.RecordTenantDatasourceResolve(stopwatch.Elapsed.TotalMilliseconds, status, sourceTag);
        }
    }

    public Task<TenantDbConnectionInfo?> ResolveAsync(
        string tenantId,
        long tenantAppInstanceId,
        CancellationToken cancellationToken = default)
    {
        return GetConnectionInfoAsync(tenantId, tenantAppInstanceId, cancellationToken);
    }

    public void Invalidate(string tenantId, long tenantAppInstanceId)
    {
        InvalidateCache(tenantId, tenantAppInstanceId);
    }

    public void InvalidateCache(string tenantId)
    {
        HybridCacheSyncBridge.Run(_cache.RemoveByTagAsync(AtlasCacheTags.TenantConnectionTenant(tenantId)));
    }

    public void InvalidateCache(string tenantId, long? tenantAppInstanceId)
    {
        if (tenantAppInstanceId.HasValue)
        {
            HybridCacheSyncBridge.Run(
                _cache.RemoveByTagAsync(AtlasCacheTags.TenantConnectionApp(tenantId, tenantAppInstanceId.Value)));
            return;
        }

        InvalidateCache(tenantId);
    }

    internal static string Encrypt(string plainText, string key)
    {
        if (string.IsNullOrEmpty(key)) return plainText;
        var keyBytes = GetKeyBytes(key);
        using var aes = Aes.Create();
        aes.Key = keyBytes;
        aes.GenerateIV();
        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encrypted = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        var result = new byte[aes.IV.Length + encrypted.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(encrypted, 0, result, aes.IV.Length, encrypted.Length);
        return Convert.ToBase64String(result);
    }

    internal static string Decrypt(string cipherText, string key)
    {
        if (string.IsNullOrEmpty(key)) return cipherText;
        var keyBytes = GetKeyBytes(key);
        var allBytes = Convert.FromBase64String(cipherText);
        using var aes = Aes.Create();
        aes.Key = keyBytes;
        var ivLength = aes.BlockSize / 8;
        var iv = new byte[ivLength];
        Buffer.BlockCopy(allBytes, 0, iv, 0, ivLength);
        var cipherBytes = new byte[allBytes.Length - ivLength];
        Buffer.BlockCopy(allBytes, ivLength, cipherBytes, 0, cipherBytes.Length);
        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor();
        var decrypted = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Encoding.UTF8.GetString(decrypted);
    }

    private static byte[] GetKeyBytes(string key)
    {
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
    }
}
