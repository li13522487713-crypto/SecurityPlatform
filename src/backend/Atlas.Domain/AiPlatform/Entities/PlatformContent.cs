using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.AiPlatform.Entities;

/// <summary>
/// 平台运营内容（PRD 01-首页）。一张通用表承载 banner / tutorial / announcement / recommended 4 类内容。
/// 字段以 ContentJson 编码，避免为每类内容单独建表；OrderIndex 控制顺序，IsActive 控制上下架。
///
/// 多租户：按 TenantId 隔离，方便不同租户配置不同首页内容。
/// </summary>
[SugarTable("PlatformContent")]
public sealed class PlatformContent : TenantEntity
{
    public PlatformContent()
        : base(TenantId.Empty)
    {
        Slot = string.Empty;
        ContentKey = string.Empty;
        ContentJson = "{}";
        Tag = string.Empty;
        CreatedAt = DateTime.UtcNow;
    }

    public PlatformContent(
        TenantId tenantId,
        string slot,
        string contentKey,
        string contentJson,
        string tag,
        int orderIndex,
        DateTimeOffset publishedAt,
        long id)
        : base(tenantId)
    {
        Id = id;
        Slot = slot;
        ContentKey = contentKey;
        ContentJson = string.IsNullOrWhiteSpace(contentJson) ? "{}" : contentJson;
        Tag = tag ?? string.Empty;
        OrderIndex = orderIndex;
        PublishedAt = publishedAt;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    /// <summary>
    /// banner / tutorial / announcement / recommended.
    /// </summary>
    [SugarColumn(Length = 32, IsNullable = false)]
    public string Slot { get; private set; }

    /// <summary>
    /// 内容业务 key（前端引用 id）。
    /// </summary>
    [SugarColumn(Length = 64, IsNullable = false)]
    public string ContentKey { get; private set; }

    [SugarColumn(ColumnDataType = "TEXT", IsNullable = false)]
    public string ContentJson { get; private set; }

    [SugarColumn(Length = 32, IsNullable = true)]
    public string Tag { get; private set; }

    public int OrderIndex { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset PublishedAt { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? UpdatedAt { get; private set; }

    public void Update(string contentJson, string tag, int orderIndex, bool isActive, DateTimeOffset publishedAt)
    {
        ContentJson = string.IsNullOrWhiteSpace(contentJson) ? "{}" : contentJson;
        Tag = tag ?? string.Empty;
        OrderIndex = orderIndex;
        IsActive = isActive;
        PublishedAt = publishedAt;
        UpdatedAt = DateTime.UtcNow;
    }
}
