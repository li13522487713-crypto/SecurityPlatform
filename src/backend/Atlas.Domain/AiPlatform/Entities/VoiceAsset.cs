using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Enums;
using SqlSugar;

namespace Atlas.Domain.AiPlatform.Entities;

/// <summary>音色资源（资源库 / Coze 对齐）。</summary>
[SugarTable("VoiceAsset")]
public sealed class VoiceAsset : TenantEntity
{
    public VoiceAsset()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        Description = string.Empty;
        Language = "zh-CN";
        Gender = "neutral";
        PreviewUrl = string.Empty;
        Source = LibrarySource.Custom;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public VoiceAsset(
        TenantId tenantId,
        string name,
        string? description,
        string language,
        string gender,
        string? previewUrl,
        LibrarySource source,
        long id)
        : base(tenantId)
    {
        Id = id;
        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        Language = string.IsNullOrWhiteSpace(language) ? "zh-CN" : language.Trim();
        Gender = string.IsNullOrWhiteSpace(gender) ? "neutral" : gender.Trim();
        PreviewUrl = previewUrl?.Trim() ?? string.Empty;
        Source = source;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    [SugarColumn(Length = 256, IsNullable = false)]
    public string Name { get; private set; }

    [SugarColumn(Length = 2000, IsNullable = true)]
    public string? Description { get; private set; }

    [SugarColumn(Length = 32, IsNullable = false)]
    public string Language { get; private set; }

    [SugarColumn(Length = 32, IsNullable = false)]
    public string Gender { get; private set; }

    [SugarColumn(Length = 2000, IsNullable = true)]
    public string? PreviewUrl { get; private set; }

    [SugarColumn(IsNullable = false)]
    public LibrarySource Source { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void Update(
        string name,
        string? description,
        string language,
        string gender,
        string? previewUrl,
        LibrarySource source)
    {
        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        Language = string.IsNullOrWhiteSpace(language) ? "zh-CN" : language.Trim();
        Gender = string.IsNullOrWhiteSpace(gender) ? "neutral" : gender.Trim();
        PreviewUrl = previewUrl?.Trim() ?? string.Empty;
        Source = source;
        UpdatedAt = DateTime.UtcNow;
    }
}
