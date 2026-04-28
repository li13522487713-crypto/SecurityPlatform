namespace Atlas.Infrastructure.Options;

/// <summary>知识库配额（等保审计配套的资源上限）。</summary>
public sealed class KnowledgeBaseQuotaOptions
{
    public int MaxPerTenant { get; set; } = 1000;

    /// <summary>单应用可绑定的知识库数量上限（在应用资源绑定 API 中校验）。</summary>
    public int MaxPerApp { get; set; } = 150;

    public int MaxDocsPerKnowledgeBase { get; set; } = 300;

    public int MaxFileSizeMB { get; set; } = 100;

    public long MaxFileSizeBytes => (long)MaxFileSizeMB * 1024 * 1024;
}
