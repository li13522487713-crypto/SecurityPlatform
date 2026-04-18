using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.LowCode.Entities;

/// <summary>
/// 应用资源引用（M01 落地，对应 docx §10.8 安全治理 #6 资源引用检查）。
/// 用于反查"哪些应用 / 页面 / 组件 引用了某个工作流 / 对话流 / 变量 / 触发器 / 数据源 / 插件 / 提示词模板"，
/// 由 M14 <c>IResourceReferenceGuardService</c> 与 <c>IResourceReferenceIndex</c> 增量维护。
/// </summary>
public sealed class AppResourceReference : TenantEntity
{
#pragma warning disable CS8618
    public AppResourceReference()
        : base(TenantId.Empty)
    {
        ResourceType = string.Empty;
        ResourceId = string.Empty;
        ReferencePath = string.Empty;
    }
#pragma warning restore CS8618

    public AppResourceReference(
        TenantId tenantId,
        long id,
        long appId,
        long? pageId,
        string? componentId,
        string resourceType,
        string resourceId,
        string referencePath)
        : base(tenantId)
    {
        Id = id;
        AppId = appId;
        PageId = pageId;
        ComponentId = componentId;
        ResourceType = resourceType;
        ResourceId = resourceId;
        ReferencePath = referencePath;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public long AppId { get; private set; }

    public long? PageId { get; private set; }

    /// <summary>组件实例 ID（PageSchema 内 ComponentSchema.id），可空表示页面级 / 应用级引用。</summary>
    [SugarColumn(Length = 128, IsNullable = true)]
    public string? ComponentId { get; private set; }

    /// <summary>资源类型：workflow / chatflow / variable / trigger / datasource / plugin / prompt-template / knowledge / database / file。</summary>
    [SugarColumn(Length = 64, IsNullable = false)]
    public string ResourceType { get; private set; }

    /// <summary>被引用资源 ID。</summary>
    [SugarColumn(Length = 128, IsNullable = false)]
    public string ResourceId { get; private set; }

    /// <summary>引用路径（JSON path，如 <c>pages[0].root.children[1].events.onClick.actions[0].workflowId</c>），用于精准定位。</summary>
    [SugarColumn(Length = 1024, IsNullable = false)]
    public string ReferencePath { get; private set; }

    /// <summary>引用版本（被引用资源的版本号，可空）。</summary>
    [SugarColumn(Length = 64, IsNullable = true)]
    public string? ResourceVersion { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public void UpdateVersion(string? resourceVersion)
    {
        ResourceVersion = resourceVersion;
    }
}
