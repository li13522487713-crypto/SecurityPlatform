using Atlas.Application.Options;
using Atlas.Application.System.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services.FileStorage;

public sealed class DynamicFileObjectStore : IFileObjectStore
{
    private readonly LocalObjectStore _localObjectStore;
    private readonly MinioObjectStore _minioObjectStore;
    private readonly AliyunOssObjectStore _aliyunOssObjectStore;
    private readonly IFileStorageSettingsResolver _settingsResolver;

    public DynamicFileObjectStore(
        LocalObjectStore localObjectStore,
        MinioObjectStore minioObjectStore,
        AliyunOssObjectStore aliyunOssObjectStore,
        IFileStorageSettingsResolver settingsResolver)
    {
        _localObjectStore = localObjectStore;
        _minioObjectStore = minioObjectStore;
        _aliyunOssObjectStore = aliyunOssObjectStore;
        _settingsResolver = settingsResolver;
    }

    public async Task SaveAsync(
        TenantId tenantId,
        string objectName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var objectStore = await ResolveObjectStoreAsync(tenantId, cancellationToken);
        await objectStore.SaveAsync(tenantId, objectName, content, contentType, cancellationToken);
    }

    public async Task<Stream> OpenReadAsync(
        TenantId tenantId,
        string objectName,
        CancellationToken cancellationToken = default)
    {
        var objectStore = await ResolveObjectStoreAsync(tenantId, cancellationToken);
        return await objectStore.OpenReadAsync(tenantId, objectName, cancellationToken);
    }

    public async Task<bool> ExistsAsync(
        TenantId tenantId,
        string objectName,
        CancellationToken cancellationToken = default)
    {
        var objectStore = await ResolveObjectStoreAsync(tenantId, cancellationToken);
        return await objectStore.ExistsAsync(tenantId, objectName, cancellationToken);
    }

    public async Task DeleteAsync(
        TenantId tenantId,
        string objectName,
        CancellationToken cancellationToken = default)
    {
        var objectStore = await ResolveObjectStoreAsync(tenantId, cancellationToken);
        await objectStore.DeleteAsync(tenantId, objectName, cancellationToken);
    }

    private async Task<IFileObjectStore> ResolveObjectStoreAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var settings = await _settingsResolver.ResolveAsync(tenantId, cancellationToken);
        return settings.Provider switch
        {
            FileStorageOptions.ProviderMinio => _minioObjectStore,
            FileStorageOptions.ProviderOss => _aliyunOssObjectStore,
            _ => _localObjectStore
        };
    }
}
