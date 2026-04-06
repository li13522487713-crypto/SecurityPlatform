using Amazon.S3;
using Amazon.S3.Model;
using Atlas.Application.Options;
using Atlas.Core.Setup;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace Atlas.Infrastructure.Services.FileStorage;

/// <summary>
/// 启动时验证对象存储（MinIO / OSS）连通性的后台服务。
/// 失败时记录 Error 日志但不阻断应用启动，避免存储节点故障导致整服不可用。
/// </summary>
public sealed class ObjectStoreConnectivityService : IHostedService
{
    private readonly IOptionsMonitor<FileStorageOptions> _optionsMonitor;
    private readonly ILogger<ObjectStoreConnectivityService> _logger;
    private readonly ISetupStateProvider _setupStateProvider;

    public ObjectStoreConnectivityService(
        IOptionsMonitor<FileStorageOptions> optionsMonitor,
        ILogger<ObjectStoreConnectivityService> logger,
        ISetupStateProvider setupStateProvider)
    {
        _optionsMonitor = optionsMonitor;
        _logger = logger;
        _setupStateProvider = setupStateProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_setupStateProvider.IsReady)
        {
            return;
        }

        var options = _optionsMonitor.CurrentValue;
        var provider = options.Provider?.Trim().ToLowerInvariant();

        if (provider == FileStorageOptions.ProviderLocal || string.IsNullOrWhiteSpace(provider))
        {
            _logger.LogInformation("文件存储：使用本地文件系统，跳过对象存储连通性验证。");
            return;
        }

        try
        {
            if (provider == FileStorageOptions.ProviderMinio)
            {
                await CheckMinioAsync(options, cancellationToken).ConfigureAwait(false);
            }
            else if (provider == FileStorageOptions.ProviderOss)
            {
                await CheckOssAsync(options, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "对象存储（provider={Provider}）连通性验证失败，服务继续启动，但文件操作可能不可用。请检查配置与网络。",
                provider);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task CheckMinioAsync(FileStorageOptions options, CancellationToken cancellationToken)
    {
        var minioOptions = options.Minio;
        var endpoint = minioOptions.Endpoint?.Trim();
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException("FileStorage:Minio:Endpoint 未配置。");
        }

        var bucketName = minioOptions.BucketName;
        if (string.IsNullOrWhiteSpace(bucketName))
        {
            throw new InvalidOperationException("FileStorage:Minio:BucketName 未配置。");
        }

        var client = new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(minioOptions.AccessKey, minioOptions.SecretKey)
            .WithSSL(minioOptions.UseSsl)
            .Build();

        var existsArgs = new BucketExistsArgs().WithBucket(bucketName);
        var exists = await client.BucketExistsAsync(existsArgs, cancellationToken).ConfigureAwait(false);

        if (!exists)
        {
            _logger.LogWarning("MinIO bucket [{Bucket}] 不存在，尝试自动创建...", bucketName);
            var makeArgs = new MakeBucketArgs().WithBucket(bucketName);
            await client.MakeBucketAsync(makeArgs, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("MinIO bucket [{Bucket}] 创建成功。endpoint={Endpoint}", bucketName, endpoint);
        }
        else
        {
            _logger.LogInformation(
                "MinIO 连通性验证通过：endpoint={Endpoint}, bucket={Bucket}",
                endpoint, bucketName);
        }
    }

    private async Task CheckOssAsync(FileStorageOptions options, CancellationToken cancellationToken)
    {
        var ossOptions = options.Oss;
        var endpoint = ossOptions.Endpoint?.Trim();
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException("FileStorage:Oss:Endpoint 未配置。");
        }

        var bucketName = ossOptions.BucketName;
        if (string.IsNullOrWhiteSpace(bucketName))
        {
            throw new InvalidOperationException("FileStorage:Oss:BucketName 未配置。");
        }

        using var client = new AmazonS3Client(
            ossOptions.AccessKeyId,
            ossOptions.AccessKeySecret,
            new AmazonS3Config
            {
                ServiceURL = endpoint,
                ForcePathStyle = ossOptions.ForcePathStyle,
                AuthenticationRegion = string.IsNullOrWhiteSpace(ossOptions.Region) ? null : ossOptions.Region
            });

        var request = new GetBucketLocationRequest { BucketName = bucketName };
        await client.GetBucketLocationAsync(request, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "OSS 连通性验证通过：endpoint={Endpoint}, bucket={Bucket}",
            endpoint, bucketName);
    }
}
