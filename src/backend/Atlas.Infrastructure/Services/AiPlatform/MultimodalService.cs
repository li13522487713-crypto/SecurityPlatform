using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;
using System.Text;
using System.Text.Json;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class MultimodalService : IMultimodalService
{
    private const string FallbackProvider = "fallback-rule-engine";

    private readonly MultimodalAssetRepository _assetRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public MultimodalService(
        MultimodalAssetRepository assetRepository,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _assetRepository = assetRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
    }

    public async Task<long> CreateAssetAsync(
        TenantId tenantId,
        long userId,
        MultimodalAssetCreateRequest request,
        CancellationToken cancellationToken)
    {
        var asset = new MultimodalAsset(
            tenantId,
            userId,
            request.AssetType,
            request.SourceType,
            request.Name?.Trim(),
            request.MimeType?.Trim(),
            request.FileId?.Trim(),
            request.SourceUrl?.Trim(),
            request.ContentText,
            request.MetadataJson,
            _idGeneratorAccessor.NextId());

        if (!string.IsNullOrWhiteSpace(request.ContentText))
        {
            asset.MarkProcessed(request.ContentText, request.MetadataJson);
        }

        await _assetRepository.AddAsync(asset, cancellationToken);
        return asset.Id;
    }

    public async Task<MultimodalAssetDto?> GetAssetAsync(
        TenantId tenantId,
        long assetId,
        CancellationToken cancellationToken)
    {
        var entity = await _assetRepository.FindByIdAsync(tenantId, assetId, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<VisionAnalyzeResult> AnalyzeVisionAsync(
        TenantId tenantId,
        long userId,
        VisionAnalyzeRequest request,
        CancellationToken cancellationToken)
    {
        var asset = await ResolveAssetAsync(tenantId, userId, request.AssetId, request.ImageUrl, MultimodalAssetType.Image, cancellationToken);
        var prompt = string.IsNullOrWhiteSpace(request.Prompt) ? "请给出图像内容摘要" : request.Prompt.Trim();
        var source = !string.IsNullOrWhiteSpace(asset.SourceUrl) ? asset.SourceUrl : $"file:{asset.FileId}";
        var detectedText = string.IsNullOrWhiteSpace(asset.ContentText) ? null : asset.ContentText;
        var summary = $"已接收图像分析请求（来源：{source}）。提示词：{prompt}。当前环境未接入视觉模型，返回规则化分析结果。";

        var metadata = JsonSerializer.Serialize(new
        {
            provider = FallbackProvider,
            prompt,
            source
        });
        asset.MarkProcessed(detectedText, metadata);
        await _assetRepository.UpdateAsync(asset, cancellationToken);

        return new VisionAnalyzeResult(summary, detectedText, FallbackProvider, asset.Id);
    }

    public async Task<AsrTranscribeResult> TranscribeAsync(
        TenantId tenantId,
        long userId,
        AsrTranscribeRequest request,
        CancellationToken cancellationToken)
    {
        var asset = await ResolveAssetAsync(tenantId, userId, request.AssetId, request.AudioUrl, MultimodalAssetType.Audio, cancellationToken);
        var language = string.IsNullOrWhiteSpace(request.LanguageHint) ? "auto" : request.LanguageHint.Trim();
        var transcript = !string.IsNullOrWhiteSpace(asset.ContentText)
            ? asset.ContentText.Trim()
            : $"[{language}] 语音内容暂未识别（环境未接入ASR模型），来源：{(!string.IsNullOrWhiteSpace(asset.SourceUrl) ? asset.SourceUrl : $"file:{asset.FileId}")}";

        var metadata = JsonSerializer.Serialize(new
        {
            provider = FallbackProvider,
            language,
            prompt = request.Prompt
        });
        asset.MarkProcessed(transcript, metadata);
        await _assetRepository.UpdateAsync(asset, cancellationToken);

        return new AsrTranscribeResult(transcript, FallbackProvider, asset.Id);
    }

    public Task<TtsSynthesizeResult> SynthesizeAsync(
        TenantId tenantId,
        long userId,
        TtsSynthesizeRequest request,
        CancellationToken cancellationToken)
    {
        _ = tenantId;
        _ = userId;
        _ = cancellationToken;

        var mimeType = NormalizeAudioMimeType(request.Format);
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(request.Text));
        var audioDataUri = $"data:{mimeType};base64,{payload}";
        var durationEstimate = Math.Max(1, (int)Math.Ceiling(request.Text.Length / 8d));

        return Task.FromResult(new TtsSynthesizeResult(
            FallbackProvider,
            audioDataUri,
            mimeType,
            durationEstimate));
    }

    private async Task<MultimodalAsset> ResolveAssetAsync(
        TenantId tenantId,
        long userId,
        long? assetId,
        string? sourceUrl,
        MultimodalAssetType expectedType,
        CancellationToken cancellationToken)
    {
        if (assetId.HasValue)
        {
            var existing = await _assetRepository.FindByIdAsync(tenantId, assetId.Value, cancellationToken)
                ?? throw new BusinessException("多模态资产不存在。", ErrorCodes.NotFound);
            if (existing.AssetType != expectedType)
            {
                throw new BusinessException("多模态资产类型不匹配。", ErrorCodes.ValidationError);
            }

            return existing;
        }

        if (string.IsNullOrWhiteSpace(sourceUrl))
        {
            throw new BusinessException("请提供资产ID或资源URL。", ErrorCodes.ValidationError);
        }

        var asset = new MultimodalAsset(
            tenantId,
            userId,
            expectedType,
            MultimodalSourceType.Url,
            null,
            null,
            null,
            sourceUrl.Trim(),
            null,
            null,
            _idGeneratorAccessor.NextId());
        await _assetRepository.AddAsync(asset, cancellationToken);
        return asset;
    }

    private static string NormalizeAudioMimeType(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return "audio/plain";
        }

        return format.Trim().ToLowerInvariant() switch
        {
            "wav" => "audio/wav",
            "mp3" => "audio/mpeg",
            "ogg" => "audio/ogg",
            _ => "audio/plain"
        };
    }

    private static MultimodalAssetDto Map(MultimodalAsset entity)
    {
        return new MultimodalAssetDto(
            entity.Id,
            entity.AssetType,
            entity.SourceType,
            entity.Status,
            entity.Name,
            entity.MimeType,
            entity.FileId,
            entity.SourceUrl,
            string.IsNullOrWhiteSpace(entity.ContentText) ? null : entity.ContentText,
            entity.MetadataJson,
            entity.CreatedByUserId,
            entity.CreatedAt,
            entity.UpdatedAt);
    }
}
