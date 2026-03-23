using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IMultimodalService
{
    Task<long> CreateAssetAsync(
        TenantId tenantId,
        long userId,
        MultimodalAssetCreateRequest request,
        CancellationToken cancellationToken);

    Task<MultimodalAssetDto?> GetAssetAsync(
        TenantId tenantId,
        long assetId,
        CancellationToken cancellationToken);

    Task<VisionAnalyzeResult> AnalyzeVisionAsync(
        TenantId tenantId,
        long userId,
        VisionAnalyzeRequest request,
        CancellationToken cancellationToken);

    Task<AsrTranscribeResult> TranscribeAsync(
        TenantId tenantId,
        long userId,
        AsrTranscribeRequest request,
        CancellationToken cancellationToken);

    Task<TtsSynthesizeResult> SynthesizeAsync(
        TenantId tenantId,
        long userId,
        TtsSynthesizeRequest request,
        CancellationToken cancellationToken);
}
