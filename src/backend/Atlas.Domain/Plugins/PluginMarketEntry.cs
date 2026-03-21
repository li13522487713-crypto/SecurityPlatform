using Atlas.Core.Plugins;

namespace Atlas.Domain.Plugins;

/// <summary>
/// 插件市场条目（可发布到市场的插件元数据）
/// </summary>
public sealed class PluginMarketEntry
{
    public long Id { get; set; }

    /// <summary>插件代码（全局唯一）</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public PluginCategory Category { get; set; }
    public string LatestVersion { get; set; } = string.Empty;
    public int Downloads { get; set; }
    public string? IconUrl { get; set; }
    public string? PackageUrl { get; set; }
    public PluginMarketStatus Status { get; set; } = PluginMarketStatus.Draft;

    /// <summary>用户评分平均分（0-5）</summary>
    public decimal AverageRating { get; set; }

    /// <summary>评分人数</summary>
    public int RatingCount { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>所有者/发布者的租户 ID</summary>
    public Guid TenantId { get; set; }
}

public enum PluginMarketStatus
{
    Draft = 0,
    Published = 1,
    Deprecated = 2
}
