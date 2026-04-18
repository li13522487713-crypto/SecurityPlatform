using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.LowCode.Entities;

/// <summary>
/// 应用模板（M07 S07-4）。
///
/// kind：page / component-set / pattern-A / pattern-B / pattern-C / pattern-D / industry。
/// shareScope：private / team / public（与提示词模板 / 插件一致的三档共享市场模型）。
/// templateJson：完整模板内容（PageSchema / 组件子树 / 模式模板配置等，由 kind 决定语义）。
/// </summary>
public sealed class AppTemplate : TenantEntity
{
#pragma warning disable CS8618
    public AppTemplate() : base(TenantId.Empty)
    {
        Code = string.Empty;
        Name = string.Empty;
        Kind = "page";
        TemplateJson = "{}";
        ShareScope = "private";
    }
#pragma warning restore CS8618

    public AppTemplate(TenantId tenantId, long id, string code, string name, string kind, string templateJson, long createdByUserId)
        : base(tenantId)
    {
        Id = id;
        Code = code;
        Name = name;
        Kind = kind;
        TemplateJson = templateJson;
        ShareScope = "private";
        CreatedByUserId = createdByUserId;
        Stars = 0;
        UseCount = 0;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    [SugarColumn(Length = 128, IsNullable = false)]
    public string Code { get; private set; }

    [SugarColumn(Length = 200, IsNullable = false)]
    public string Name { get; private set; }

    /// <summary>page / component-set / pattern-A / pattern-B / pattern-C / pattern-D / industry。</summary>
    [SugarColumn(Length = 32, IsNullable = false)]
    public string Kind { get; private set; }

    [SugarColumn(Length = 1000, IsNullable = true)]
    public string? Description { get; private set; }

    /// <summary>行业标签：ecommerce / customer-service / content-generation / data-analysis（kind=industry 时使用）。</summary>
    [SugarColumn(Length = 64, IsNullable = true)]
    public string? IndustryTag { get; private set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = false)]
    public string TemplateJson { get; private set; }

    /// <summary>共享范围：private / team / public。</summary>
    [SugarColumn(Length = 32, IsNullable = false)]
    public string ShareScope { get; private set; }

    /// <summary>市场点赞数（M07 S07-4）。</summary>
    public int Stars { get; private set; }

    /// <summary>使用次数（apply 调用 +1）。</summary>
    public int UseCount { get; private set; }

    public long CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(string name, string kind, string? description, string? industryTag, string templateJson, string? shareScope)
    {
        Name = name;
        Kind = kind;
        Description = description;
        IndustryTag = industryTag;
        TemplateJson = templateJson;
        ShareScope = shareScope ?? ShareScope;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void IncrementStars() { Stars += 1; UpdatedAt = DateTimeOffset.UtcNow; }
    public void DecrementStars() { Stars = Math.Max(0, Stars - 1); UpdatedAt = DateTimeOffset.UtcNow; }
    public void IncrementUse() { UseCount += 1; UpdatedAt = DateTimeOffset.UtcNow; }
}
