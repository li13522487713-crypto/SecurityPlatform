using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.LowCode.Entities;

/// <summary>
/// 低代码应用定义聚合根（M01 落地）。
/// 与前端 <c>@atlas/lowcode-schema</c> 中的 <c>AppSchema</c> 一一对应。
///
/// 设计说明：
/// - 聚合根包含完整的 schema JSON 草稿（<see cref="DraftSchemaJson"/>）与"当前生效版本号"指针；
///   完整版本归档落在 <see cref="AppVersionArchive"/>。
/// - schema JSON 列含敏感配置（密钥占位 / Webhook URL 等）时按租户密钥加密；
///   M01 阶段统一为 <c>text</c>，加密由 M14 / M17 联动落地。
/// - 多端类型（web / mini_program / hybrid）以字符串枚举存储，避免数据库迁移负担。
/// </summary>
public sealed class AppDefinition : TenantEntity
{
#pragma warning disable CS8618
    public AppDefinition()
        : base(TenantId.Empty)
    {
        Code = string.Empty;
        DisplayName = string.Empty;
        SchemaVersion = "v1";
        DraftSchemaJson = "{}";
        TargetTypes = "web";
        DefaultLocale = "zh-CN";
        Status = "draft";
    }
#pragma warning restore CS8618

    public AppDefinition(
        TenantId tenantId,
        long id,
        string code,
        string displayName,
        string targetTypes,
        string? defaultLocale)
        : base(tenantId)
    {
        Id = id;
        Code = code;
        DisplayName = displayName;
        TargetTypes = string.IsNullOrWhiteSpace(targetTypes) ? "web" : targetTypes;
        DefaultLocale = string.IsNullOrWhiteSpace(defaultLocale) ? "zh-CN" : defaultLocale!;
        SchemaVersion = "v1";
        DraftSchemaJson = "{}";
        Status = "draft";
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    /// <summary>应用编码（租户内唯一），用于运行时按编码定位（与前端路由 <c>/apps/lowcode/:code</c> 对齐）。</summary>
    [SugarColumn(Length = 128, IsNullable = false)]
    public string Code { get; private set; }

    /// <summary>显示名（租户内可重复）。</summary>
    [SugarColumn(Length = 200, IsNullable = false)]
    public string DisplayName { get; private set; }

    /// <summary>简介（用于资源列表展示与共享市场）。</summary>
    [SugarColumn(Length = 2000, IsNullable = true)]
    public string? Description { get; private set; }

    /// <summary>Schema 版本号（v1 / v2 ...），与前端 <c>schemaVersion</c> 严格对齐；不允许为空。</summary>
    [SugarColumn(Length = 16, IsNullable = false)]
    public string SchemaVersion { get; private set; }

    /// <summary>多端类型（逗号分隔：web / mini_program / hybrid）；至少包含 web。</summary>
    [SugarColumn(Length = 64, IsNullable = false)]
    public string TargetTypes { get; private set; }

    /// <summary>默认语言（与前端 i18n 对齐：zh-CN / en-US）。</summary>
    [SugarColumn(Length = 16, IsNullable = false)]
    public string DefaultLocale { get; private set; }

    /// <summary>主题配置（可选，对齐 docx §10.2.1 AppSchema.theme，存 JSON）。</summary>
    [SugarColumn(IsJson = true, ColumnDataType = "text", IsNullable = true)]
    public AppThemeConfig? Theme { get; private set; }

    /// <summary>
    /// 草稿 Schema JSON（完整 AppSchema），与前端 <c>AppSchema</c> 镜像；包含 pages / variables / contentParams / lifecycle / metadata 等。
    /// 草稿是最近一次自动保存或手动保存的状态；版本归档另由 <see cref="AppVersionArchive"/> 管理。
    /// </summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = false)]
    public string DraftSchemaJson { get; private set; }

    /// <summary>当前生效版本 ID（指向 <see cref="AppVersionArchive.Id"/>）；草稿态可空。</summary>
    public long? CurrentVersionId { get; private set; }

    /// <summary>状态：draft / published / archived。</summary>
    [SugarColumn(Length = 32, IsNullable = false)]
    public string Status { get; private set; }

    /// <summary>创建时间（带时区）。</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>最后更新时间。</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>创建者用户 ID（用于数据范围过滤，等保访问控制）。</summary>
    public long? CreatedByUserId { get; private set; }

    /// <summary>最近编辑者用户 ID。</summary>
    public long? UpdatedByUserId { get; private set; }

    public void SetCreatedByUser(long userId)
    {
        CreatedByUserId = userId;
        UpdatedByUserId = userId;
    }

    public void UpdateMetadata(string displayName, string? description, string targetTypes, string defaultLocale, AppThemeConfig? theme, long updatedByUserId)
    {
        DisplayName = displayName;
        Description = description;
        TargetTypes = string.IsNullOrWhiteSpace(targetTypes) ? "web" : targetTypes;
        DefaultLocale = string.IsNullOrWhiteSpace(defaultLocale) ? "zh-CN" : defaultLocale;
        Theme = theme;
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedByUserId = updatedByUserId;
    }

    public void ReplaceDraftSchema(string draftSchemaJson, long updatedByUserId)
    {
        if (string.IsNullOrWhiteSpace(draftSchemaJson))
        {
            throw new ArgumentException("draftSchemaJson 不可为空", nameof(draftSchemaJson));
        }

        DraftSchemaJson = draftSchemaJson;
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedByUserId = updatedByUserId;
    }

    public void BindCurrentVersion(long versionId)
    {
        CurrentVersionId = versionId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkPublished(long versionId, long updatedByUserId)
    {
        Status = "published";
        CurrentVersionId = versionId;
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedByUserId = updatedByUserId;
    }

    public void MarkArchived(long updatedByUserId)
    {
        Status = "archived";
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedByUserId = updatedByUserId;
    }
}

/// <summary>
/// 应用主题配置（对齐 docx §10.2.1 AppSchema.theme），仅作为 JSON 子结构存储；具体字段允许由 M07 / M17 扩展。
/// </summary>
public sealed class AppThemeConfig
{
    /// <summary>主色（hex / hsl / oklch 任意值，由前端解析）。</summary>
    public string? PrimaryColor { get; set; }

    /// <summary>圆角基础值（px）。</summary>
    public int? BorderRadius { get; set; }

    /// <summary>暗色模式策略：never / always / auto。</summary>
    public string? DarkMode { get; set; }

    /// <summary>自定义 CSS 变量字典（key → value）。</summary>
    public Dictionary<string, string>? CssVariables { get; set; }
}
