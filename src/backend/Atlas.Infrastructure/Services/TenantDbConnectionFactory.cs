using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Infrastructure.Observability;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SqlSugar;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 租户数据库连接工厂实现（等保2.0 数据隔离）
/// <para>从数据库读取租户自定义数据源，连接字符串使用 AES-256 加密存储。</para>
/// </summary>
public sealed class TenantDbConnectionFactory : ITenantDbConnectionFactory
{
    private readonly TenantDataSourceRepository _repository;
    private readonly IMemoryCache _cache;
    private readonly DatabaseEncryptionOptions _encryptionOptions;
    private const string CacheKeyPrefix = "tenant-conn:";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public TenantDbConnectionFactory(
        TenantDataSourceRepository repository,
        IMemoryCache cache,
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
        var cacheKey = CacheKeyPrefix + tenantId;
        if (_cache.TryGetValue(cacheKey, out TenantDbConnectionInfo? cached))
        {
            AtlasMetrics.RecordTenantDatasourceResolve(0, "success", "cache");
            return cached;
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
                _cache.Set(cacheKey, (TenantDbConnectionInfo?)null, CacheDuration);
                return null;
            }

            var connectionString = _encryptionOptions.Enabled
                ? Decrypt(source.EncryptedConnectionString, _encryptionOptions.Key)
                : source.EncryptedConnectionString;

            var info = new TenantDbConnectionInfo(connectionString, source.DbType);
            _cache.Set(cacheKey, info, CacheDuration);
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

    public void InvalidateCache(string tenantId)
    {
        _cache.Remove(CacheKeyPrefix + tenantId);
    }

    public static string Encrypt(string plainText, string key)
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

    public static string Decrypt(string cipherText, string key)
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

    public static DbType ParseDbType(string dbType)
    {
        return dbType.Trim().ToLowerInvariant() switch
        {
            "sqlite" => DbType.Sqlite,
            "sqlserver" => DbType.SqlServer,
            "mysql" => DbType.MySql,
            "postgresql" => DbType.PostgreSQL,
            _ => DbType.Sqlite
        };
    }

    public static async Task<bool> TestConnectionAsync(
        string connectionString,
        string dbType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = connectionString,
                DbType = ParseDbType(dbType),
                IsAutoCloseConnection = true
            });

            _ = await client.Ado.GetIntAsync("SELECT 1");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static byte[] GetKeyBytes(string key)
    {
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
    }
}
