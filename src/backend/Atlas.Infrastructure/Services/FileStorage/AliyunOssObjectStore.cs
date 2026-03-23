using Amazon.S3;
using Amazon.S3.Model;
using Atlas.Application.Options;
using Atlas.Application.System.Abstractions;
using Atlas.Core.Tenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Services.FileStorage;

/// <summary>
/// 阿里云 OSS 对象存储实现，通过 S3 兼容接口（AWSSDK.S3）接入。
/// IAmazonS3 配置为指向 OSS S3 兼容 endpoint 的 singleton。
/// </summary>
public sealed class AliyunOssObjectStore : IFileObjectStore
{
    private readonly IAmazonS3 _client;
    private readonly string _bucketName;
    private readonly ILogger<AliyunOssObjectStore> _logger;

    public AliyunOssObjectStore(
        IAmazonS3 client,
        IOptions<FileStorageOptions> options,
        ILogger<AliyunOssObjectStore> logger)
    {
        _client = client;
        _bucketName = options.Value.Oss.BucketName;
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
        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false
        };
        await _client.PutObjectAsync(request, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("OSS PutObject 成功：bucket={Bucket}, key={Key}", _bucketName, key);
    }

    public async Task<Stream> OpenReadAsync(
        TenantId tenantId,
        string objectName,
        CancellationToken cancellationToken = default)
    {
        var key = BuildKey(tenantId, objectName);
        var request = new GetObjectRequest
        {
            BucketName = _bucketName,
            Key = key
        };
        var response = await _client.GetObjectAsync(request, cancellationToken).ConfigureAwait(false);

        // 将响应流复制到 MemoryStream，以便调用方可 Seek（支持 Range 下载）
        var buffer = new MemoryStream();
        await response.ResponseStream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
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
            var request = new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = key
            };
            await _client.GetObjectMetadataAsync(request, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
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
        var request = new DeleteObjectRequest
        {
            BucketName = _bucketName,
            Key = key
        };
        await _client.DeleteObjectAsync(request, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("OSS DeleteObject 成功：bucket={Bucket}, key={Key}", _bucketName, key);
    }

    private static string BuildKey(TenantId tenantId, string objectName) =>
        $"{tenantId}/{objectName}";
}
