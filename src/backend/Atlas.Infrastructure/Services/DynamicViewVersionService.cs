using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Atlas.Application.DynamicViews.Models;
using Atlas.Application.DynamicViews.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicViews.Entities;

namespace Atlas.Infrastructure.Services;

public sealed class DynamicViewVersionService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IDynamicViewVersionRepository _versionRepository;
    private readonly TimeProvider _timeProvider;
    private readonly Atlas.Core.Abstractions.IIdGeneratorAccessor _idGeneratorAccessor;

    public DynamicViewVersionService(
        IDynamicViewVersionRepository versionRepository,
        TimeProvider timeProvider,
        Atlas.Core.Abstractions.IIdGeneratorAccessor idGeneratorAccessor)
    {
        _versionRepository = versionRepository;
        _timeProvider = timeProvider;
        _idGeneratorAccessor = idGeneratorAccessor;
    }

    public async Task<DynamicViewPublishResultDto> CreateVersionAsync(
        TenantId tenantId,
        long? appId,
        string viewKey,
        DynamicViewCreateOrUpdateRequest definition,
        long userId,
        string status,
        string? comment,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var nextVersion = await _versionRepository.GetLatestVersionAsync(tenantId, appId, viewKey, cancellationToken) + 1;
        var definitionJson = JsonSerializer.Serialize(definition, JsonOptions);
        var checksum = ComputeChecksum(definitionJson);

        var versionEntity = new DynamicViewVersion(
            tenantId,
            _idGeneratorAccessor.NextId(),
            appId,
            viewKey,
            nextVersion,
            definitionJson,
            checksum,
            status,
            comment,
            userId,
            now);

        await _versionRepository.AddAsync(versionEntity, cancellationToken);
        return new DynamicViewPublishResultDto(viewKey, nextVersion, now, checksum);
    }

    public static string ComputeChecksum(string payload)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes);
    }
}
