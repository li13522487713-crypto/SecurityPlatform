using Atlas.Application.Options;
using Atlas.Application.System.Abstractions;
using Atlas.Core.Tenancy;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Services.FileStorage;

public sealed class LocalObjectStore : IFileObjectStore
{
    private readonly FileStorageOptions _options;
    private readonly IHostEnvironmentAccessor _hostEnvironmentAccessor;

    public LocalObjectStore(
        IOptions<FileStorageOptions> options,
        IHostEnvironmentAccessor hostEnvironmentAccessor)
    {
        _options = options.Value;
        _hostEnvironmentAccessor = hostEnvironmentAccessor;
    }

    public async Task SaveAsync(
        TenantId tenantId,
        string objectName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var directory = GetTenantDirectory(tenantId);
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
        var fullPath = Path.Combine(GetTenantDirectory(tenantId), objectName);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"对象不存在: {objectName}", fullPath);
        }

        Stream stream = File.OpenRead(fullPath);
        return Task.FromResult(stream);
    }

    public Task<bool> ExistsAsync(
        TenantId tenantId,
        string objectName,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(GetTenantDirectory(tenantId), objectName);
        return Task.FromResult(File.Exists(fullPath));
    }

    public Task DeleteAsync(
        TenantId tenantId,
        string objectName,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(GetTenantDirectory(tenantId), objectName);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    private string GetTenantDirectory(TenantId tenantId)
    {
        return Path.Combine(
            _hostEnvironmentAccessor.ContentRootPath,
            _options.BasePath,
            tenantId.Value.ToString());
    }
}
