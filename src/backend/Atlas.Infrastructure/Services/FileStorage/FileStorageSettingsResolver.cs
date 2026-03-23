using Atlas.Application.Options;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Repositories;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Services.FileStorage;

public sealed record FileStorageEffectiveSettings(
    FileStorageOptions Options,
    string Provider,
    string BasePath,
    string MinioBucketName,
    string? AppId);

public interface IFileStorageSettingsResolver
{
    Task<FileStorageEffectiveSettings> ResolveAsync(TenantId tenantId, CancellationToken cancellationToken = default);
}

public sealed class FileStorageSettingsResolver : IFileStorageSettingsResolver
{
    private static readonly string[] OverrideKeys =
    [
        "FileStorage:BasePath",
        "FileStorage:Minio:BucketName"
    ];

    private readonly IOptionsMonitor<FileStorageOptions> _optionsMonitor;
    private readonly SystemConfigRepository _systemConfigRepository;
    private readonly IAppContextAccessor _appContextAccessor;

    public FileStorageSettingsResolver(
        IOptionsMonitor<FileStorageOptions> optionsMonitor,
        SystemConfigRepository systemConfigRepository,
        IAppContextAccessor appContextAccessor)
    {
        _optionsMonitor = optionsMonitor;
        _systemConfigRepository = systemConfigRepository;
        _appContextAccessor = appContextAccessor;
    }

    public async Task<FileStorageEffectiveSettings> ResolveAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var options = _optionsMonitor.CurrentValue;
        var provider = options.Provider?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(provider))
        {
            provider = FileStorageOptions.ProviderLocal;
        }

        var appId = NormalizeAppId(_appContextAccessor.GetAppId());
        var overrides = await _systemConfigRepository.GetByKeysAsync(tenantId, OverrideKeys, appId, cancellationToken);
        var overrideMap = overrides.ToDictionary(static x => x.ConfigKey, static x => x.ConfigValue, StringComparer.OrdinalIgnoreCase);

        var basePath = options.BasePath;
        if (overrideMap.TryGetValue("FileStorage:BasePath", out var basePathValue)
            && !string.IsNullOrWhiteSpace(basePathValue))
        {
            basePath = basePathValue.Trim();
        }

        var minioBucketName = options.Minio.BucketName;
        if (overrideMap.TryGetValue("FileStorage:Minio:BucketName", out var bucketNameValue)
            && !string.IsNullOrWhiteSpace(bucketNameValue))
        {
            minioBucketName = bucketNameValue.Trim();
        }

        return new FileStorageEffectiveSettings(options, provider, basePath, minioBucketName, appId);
    }

    private static string? NormalizeAppId(string? appId)
    {
        return string.IsNullOrWhiteSpace(appId) ? null : appId.Trim();
    }
}
