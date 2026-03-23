using Amazon.S3;
using Amazon.S3.Model;
using Atlas.Application.Options;
using Atlas.Application.System.Abstractions;
using Atlas.Core.Tenancy;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.FileStorage;

/// <summary>
/// 阿里云 OSS 对象存储实现，通过 S3 兼容接口（AWSSDK.S3）接入。
/// 每次请求按当前配置动态创建客户端，支持配置热更新。
/// </summary>
public sealed class AliyunOssObjectStore : IFileObjectStore
{
    private readonly IFileStorageSettingsResolver _settingsResolver;
    private readonly ILogger<AliyunOssObjectStore> _logger;

    public AliyunOssObjectStore(
        IFileStorageSettingsResolver settingsResolver,
        ILogger<AliyunOssObjectStore> logger)
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
        var client = CreateClient(settings.Options.Oss);
        var bucketName = settings.Options.Oss.BucketName;
        var key = BuildKey(tenantId, objectName);
        var request = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false
        };
        await client.PutObjectAsync(request, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("OSS PutObject 成功：bucket={Bucket}, key={Key}", bucketName, key);
    }

    public async Task<Stream> OpenReadAsync(
        TenantId tenantId,
        string objectName,
        CancellationToken cancellationToken = default)
    {
        var settings = await _settingsResolver.ResolveAsync(tenantId, cancellationToken);
        var client = CreateClient(settings.Options.Oss);
        var bucketName = settings.Options.Oss.BucketName;
        var key = BuildKey(tenantId, objectName);
        var request = new GetObjectRequest
        {
            BucketName = bucketName,
            Key = key
        };
        var response = await client.GetObjectAsync(request, cancellationToken).ConfigureAwait(false);

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
        var settings = await _settingsResolver.ResolveAsync(tenantId, cancellationToken);
        var client = CreateClient(settings.Options.Oss);
        var bucketName = settings.Options.Oss.BucketName;
        var key = BuildKey(tenantId, objectName);
        try
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = bucketName,
                Key = key
            };
            await client.GetObjectMetadataAsync(request, cancellationToken).ConfigureAwait(false);
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
        var settings = await _settingsResolver.ResolveAsync(tenantId, cancellationToken);
        var client = CreateClient(settings.Options.Oss);
        var bucketName = settings.Options.Oss.BucketName;
        var key = BuildKey(tenantId, objectName);
        var request = new DeleteObjectRequest
        {
            BucketName = bucketName,
            Key = key
        };
        await client.DeleteObjectAsync(request, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("OSS DeleteObject 成功：bucket={Bucket}, key={Key}", bucketName, key);
    }

    private static IAmazonS3 CreateClient(OssStorageOptions options)
    {
        var endpoint = options.Endpoint?.Trim();
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException("FileStorage:Oss:Endpoint 未配置。");
        }

        var config = new AmazonS3Config
        {
            ServiceURL = endpoint,
            ForcePathStyle = options.ForcePathStyle,
            AuthenticationRegion = string.IsNullOrWhiteSpace(options.Region) ? null : options.Region
        };

        return new AmazonS3Client(options.AccessKeyId, options.AccessKeySecret, config);
    }

    private static string BuildKey(TenantId tenantId, string objectName) =>
        $"{tenantId}/{objectName}";
}
