using Atlas.Application.Options;
using Atlas.Application.System.Abstractions;
using Atlas.Core.Tenancy;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace Atlas.Infrastructure.Services.FileStorage;

/// <summary>
/// MinIO 对象存储实现，使用官方 Minio .NET SDK（7.x）。
/// 每次请求按当前配置动态创建客户端，支持配置热更新。
/// </summary>
public sealed class MinioObjectStore : IFileObjectStore
{
    private readonly IFileStorageSettingsResolver _settingsResolver;
    private readonly ILogger<MinioObjectStore> _logger;

    public MinioObjectStore(
        IFileStorageSettingsResolver settingsResolver,
        ILogger<MinioObjectStore> logger)
    {
        _settingsResolver = settingsResolver;
        _logger = logger;
    }

    public async Task SaveAsync(
        TenantId tenantId,
        string objectName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var settings = await _settingsResolver.ResolveAsync(tenantId, cancellationToken);
        var client = CreateClient(settings.Options.Minio);
        var bucketName = settings.MinioBucketName;
        var key = BuildKey(tenantId, objectName);
        var size = content.CanSeek ? content.Length - content.Position : -1;

        var args = new PutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(key)
            .WithStreamData(content)
            .WithObjectSize(size)
            .WithContentType(contentType);

        await client.PutObjectAsync(args, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("MinIO PutObject 成功：bucket={Bucket}, key={Key}", bucketName, key);
    }

    public async Task<Stream> OpenReadAsync(
        TenantId tenantId,
        string objectName,
        CancellationToken cancellationToken = default)
    {
        var settings = await _settingsResolver.ResolveAsync(tenantId, cancellationToken);
        var client = CreateClient(settings.Options.Minio);
        var bucketName = settings.MinioBucketName;
        var key = BuildKey(tenantId, objectName);
        var buffer = new MemoryStream();

        var args = new GetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(key)
            .WithCallbackStream((stream, ct) => stream.CopyToAsync(buffer, ct));

        await client.GetObjectAsync(args, cancellationToken).ConfigureAwait(false);
        buffer.Position = 0;
        return buffer;
    }

    public async Task<bool> ExistsAsync(
        TenantId tenantId,
        string objectName,
        CancellationToken cancellationToken = default)
    {
        var settings = await _settingsResolver.ResolveAsync(tenantId, cancellationToken);
        var client = CreateClient(settings.Options.Minio);
        var bucketName = settings.MinioBucketName;
        var key = BuildKey(tenantId, objectName);
        try
        {
            var args = new StatObjectArgs()
                .WithBucket(bucketName)
                .WithObject(key);
            await client.StatObjectAsync(args, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (ObjectNotFoundException)
        {
            return false;
        }
        catch (BucketNotFoundException)
        {
            return false;
        }
    }

    public async Task DeleteAsync(
        TenantId tenantId,
        string objectName,
        CancellationToken cancellationToken = default)
    {
        var settings = await _settingsResolver.ResolveAsync(tenantId, cancellationToken);
        var client = CreateClient(settings.Options.Minio);
        var bucketName = settings.MinioBucketName;
        var key = BuildKey(tenantId, objectName);
        var args = new RemoveObjectArgs()
            .WithBucket(bucketName)
            .WithObject(key);
        await client.RemoveObjectAsync(args, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("MinIO RemoveObject 成功：bucket={Bucket}, key={Key}", bucketName, key);
    }

    private IMinioClient CreateClient(MinioStorageOptions options)
    {
        var endpoint = options.Endpoint?.Trim();
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException("FileStorage:Minio:Endpoint 未配置。");
        }

        return new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(options.AccessKey, options.SecretKey)
            .WithSSL(options.UseSsl)
            .Build();
    }

    private static string BuildKey(TenantId tenantId, string objectName) =>
        $"{tenantId}/{objectName}";
}
