using Atlas.Application.Options;
using Atlas.Application.System.Abstractions;
using Atlas.Core.Tenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace Atlas.Infrastructure.Services.FileStorage;

/// <summary>
/// MinIO 对象存储实现，使用官方 Minio .NET SDK（7.x）。
/// 通过 IMinioClient（singleton）执行对象操作；bucket 不存在时由
/// ObjectStoreConnectivityService 在启动阶段自动创建。
/// </summary>
public sealed class MinioObjectStore : IFileObjectStore
{
    private readonly IMinioClient _client;
    private readonly string _bucketName;
    private readonly ILogger<MinioObjectStore> _logger;

    public MinioObjectStore(
        IMinioClient client,
        IOptions<FileStorageOptions> options,
        ILogger<MinioObjectStore> logger)
    {
        _client = client;
        _bucketName = options.Value.Minio.BucketName;
        _logger = logger;
    }

    public async Task SaveAsync(
        TenantId tenantId,
        string objectName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var key = BuildKey(tenantId, objectName);
        var size = content.CanSeek ? content.Length - content.Position : -1;

        var args = new PutObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(key)
            .WithStreamData(content)
            .WithObjectSize(size)
            .WithContentType(contentType);

        await _client.PutObjectAsync(args, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("MinIO PutObject 成功：bucket={Bucket}, key={Key}", _bucketName, key);
    }

    public async Task<Stream> OpenReadAsync(
        TenantId tenantId,
        string objectName,
        CancellationToken cancellationToken = default)
    {
        var key = BuildKey(tenantId, objectName);
        var buffer = new MemoryStream();

        var args = new GetObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(key)
            .WithCallbackStream((stream, ct) => stream.CopyToAsync(buffer, ct));

        await _client.GetObjectAsync(args, cancellationToken).ConfigureAwait(false);
        buffer.Position = 0;
        return buffer;
    }

    public async Task<bool> ExistsAsync(
        TenantId tenantId,
        string objectName,
        CancellationToken cancellationToken = default)
    {
        var key = BuildKey(tenantId, objectName);
        try
        {
            var args = new StatObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(key);
            await _client.StatObjectAsync(args, cancellationToken).ConfigureAwait(false);
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
        var key = BuildKey(tenantId, objectName);
        var args = new RemoveObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(key);
        await _client.RemoveObjectAsync(args, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("MinIO RemoveObject 成功：bucket={Bucket}, key={Key}", _bucketName, key);
    }

    private static string BuildKey(TenantId tenantId, string objectName) =>
        $"{tenantId}/{objectName}";
}
