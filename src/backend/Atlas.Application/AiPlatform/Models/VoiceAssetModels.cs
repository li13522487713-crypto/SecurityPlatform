namespace Atlas.Application.AiPlatform.Models;

public sealed record VoiceAssetCreateRequest(
    string Name,
    string? Description,
    string? Language,
    string? Gender,
    string? PreviewUrl);

public sealed record VoiceAssetCreatedDto(long Id);
