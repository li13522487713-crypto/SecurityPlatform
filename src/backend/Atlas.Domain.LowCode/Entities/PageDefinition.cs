using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.LowCode.Entities;

/// <summary>
/// 页面定义（M01 落地，对应 docx §10.2.2 PageSchema）。
/// 一个 <see cref="AppDefinition"/> 可挂多个 <see cref="PageDefinition"/>，按 <see cref="OrderNo"/> 排序。
/// </summary>
public sealed class PageDefinition : TenantEntity
{
#pragma warning disable CS8618
    public PageDefinition()
        : base(TenantId.Empty)
    {
        Code = string.Empty;
        DisplayName = string.Empty;
        Path = string.Empty;
        TargetType = "web";
        Layout = "free";
        SchemaJson = "{}";
    }
#pragma warning restore CS8618

    public PageDefinition(
        TenantId tenantId,
        long id,
        long appId,
        string code,
        string displayName,
        string path,
        string targetType,
        string layout,
        int orderNo)
        : base(tenantId)
    {
        Id = id;
        AppId = appId;
        Code = code;
        DisplayName = displayName;
        Path = path;
        TargetType = string.IsNullOrWhiteSpace(targetType) ? "web" : targetType;
        Layout = string.IsNullOrWhiteSpace(layout) ? "free" : layout;
        OrderNo = orderNo;
        SchemaJson = "{}";
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public long AppId { get; private set; }

    /// <summary>页面编码（应用内唯一）。</summary>
    [SugarColumn(Length = 128, IsNullable = false)]
    public string Code { get; private set; }

    [SugarColumn(Length = 200, IsNullable = false)]
    public string DisplayName { get; private set; }

    /// <summary>路由路径（应用内相对路径，如 /home、/orders/:id）。</summary>
    [SugarColumn(Length = 256, IsNullable = false)]
    public string Path { get; private set; }

    /// <summary>多端类型：web / mini_program / hybrid。</summary>
    [SugarColumn(Length = 32, IsNullable = false)]
    public string TargetType { get; private set; }

    /// <summary>布局策略：free（自由）/ flow（流式）/ responsive（响应式）；docx §10.2.2 PageSchema.layout。</summary>
    [SugarColumn(Length = 32, IsNullable = false)]
    public string Layout { get; private set; }

    /// <summary>页面排序（应用内）。</summary>
    public int OrderNo { get; private set; }

    /// <summary>是否可见（隐藏页可作为子流程或弹窗）。</summary>
    public bool IsVisible { get; private set; } = true;

    /// <summary>是否锁定（编辑时禁止修改）。</summary>
    public bool IsLocked { get; private set; }

    /// <summary>页面 Schema JSON（完整 PageSchema，含 root ComponentSchema、生命周期、page-scope 变量等）。</summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = false)]
    public string SchemaJson { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void UpdateMetadata(string displayName, string path, string targetType, string layout, bool isVisible, bool isLocked)
    {
        DisplayName = displayName;
        Path = path;
        TargetType = string.IsNullOrWhiteSpace(targetType) ? "web" : targetType;
        Layout = string.IsNullOrWhiteSpace(layout) ? "free" : layout;
        IsVisible = isVisible;
        IsLocked = isLocked;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Reorder(int orderNo)
    {
        OrderNo = orderNo;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ReplaceSchema(string schemaJson)
    {
        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            throw new ArgumentException("schemaJson 不可为空", nameof(schemaJson));
        }

        SchemaJson = schemaJson;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
