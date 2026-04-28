using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IVoiceAssetService
{
    Task<VoiceAssetCreatedDto> CreateAsync(
        TenantId tenantId,
        VoiceAssetCreateRequest request,
        CancellationToken cancellationToken);
}
