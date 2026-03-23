using Atlas.Application.System.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services.FileStorage;

public sealed class LocalObjectStore : IFileObjectStore
{
    private readonly IFileStorageSettingsResolver _settingsResolver;
    private readonly IHostEnvironmentAccessor _hostEnvironmentAccessor;

    public LocalObjectStore(
        IFileStorageSettingsResolver settingsResolver,
        IHostEnvironmentAccessor hostEnvironmentAccessor)
    {
        _settingsResolver = settingsResolver;
        _hostEnvironmentAccessor = hostEnvironmentAccessor;
    }

    public async Task SaveAsync(
        TenantId tenantId,
        string objectName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var directory = await GetTenantDirectoryAsync(tenantId, cancellationToken);
        Directory.CreateDirectory(directory);
        var fullPath = Path.Combine(directory, objectName);
        await using var destination = File.Create(fullPath);
        await content.CopyToAsync(destination, cancellationToken);
    }

    public Task<Stream> OpenReadAsync(
        TenantId tenantId,
        string objectName,
        CancellationToken cancellationToken = default)
    {
        return OpenReadInternalAsync(tenantId, objectName, cancellationToken);
    }

    public async Task<bool> ExistsAsync(
        TenantId tenantId,
        string objectName,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(await GetTenantDirectoryAsync(tenantId, cancellationToken), objectName);
        return File.Exists(fullPath);
    }

    public async Task DeleteAsync(
        TenantId tenantId,
        string objectName,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(await GetTenantDirectoryAsync(tenantId, cancellationToken), objectName);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    private async Task<Stream> OpenReadInternalAsync(
        TenantId tenantId,
        string objectName,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.Combine(await GetTenantDirectoryAsync(tenantId, cancellationToken), objectName);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"对象不存在: {objectName}", fullPath);
        }

        return File.OpenRead(fullPath);
    }

    private async Task<string> GetTenantDirectoryAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var settings = await _settingsResolver.ResolveAsync(tenantId, cancellationToken);
        return Path.Combine(
            _hostEnvironmentAccessor.ContentRootPath,
            settings.BasePath,
            tenantId.Value.ToString());
    }
}
