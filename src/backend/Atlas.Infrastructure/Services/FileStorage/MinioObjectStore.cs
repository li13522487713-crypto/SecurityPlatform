using Atlas.Application.System.Abstractions;
using Atlas.Core.Tenancy;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.FileStorage;

public sealed class MinioObjectStore : IFileObjectStore
{
    private readonly LocalObjectStore _localFallback;
    private readonly ILogger<MinioObjectStore> _logger;
    private int _warned;

    public MinioObjectStore(
        LocalObjectStore localFallback,
        ILogger<MinioObjectStore> logger)
    {
        _localFallback = localFallback;
        _logger = logger;
    }

    public async Task SaveAsync(
        TenantId tenantId,
        string objectName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        WarnAndFallbackOnce();
        await _localFallback.SaveAsync(tenantId, objectName, content, contentType, cancellationToken);
    }

    public async Task<Stream> OpenReadAsync(
        TenantId tenantId,
        string objectName,
        CancellationToken cancellationToken = default)
    {
        WarnAndFallbackOnce();
        return await _localFallback.OpenReadAsync(tenantId, objectName, cancellationToken);
    }

    public async Task<bool> ExistsAsync(
        TenantId tenantId,
        string objectName,
        CancellationToken cancellationToken = default)
    {
        WarnAndFallbackOnce();
        return await _localFallback.ExistsAsync(tenantId, objectName, cancellationToken);
    }

    public async Task DeleteAsync(
        TenantId tenantId,
        string objectName,
        CancellationToken cancellationToken = default)
    {
        WarnAndFallbackOnce();
        await _localFallback.DeleteAsync(tenantId, objectName, cancellationToken);
    }

    private void WarnAndFallbackOnce()
    {
        if (Interlocked.CompareExchange(ref _warned, 1, 0) == 0)
        {
            _logger.LogWarning("当前运行环境未启用 MinIO SDK，已自动回退为本地文件存储实现。");
        }
    }
}
