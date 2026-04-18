using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.LowCode.Entities;

/// <summary>
/// 内容参数（M01 落地，对应 docx §U11 ContentParam 6 类独立机制）。
/// 区别于一般 BindingSchema：内容参数是组件接收"内容"（文案 / 图片 / 数据 / 链接 / 媒体 / AI 内容）的统一抽象。
/// 完整规格见 <c>docs/lowcode-content-params-spec.md</c>。
/// </summary>
public sealed class AppContentParam : TenantEntity
{
#pragma warning disable CS8618
    public AppContentParam()
        : base(TenantId.Empty)
    {
        Code = string.Empty;
        Kind = "text";
        ConfigJson = "{}";
    }
#pragma warning restore CS8618

    public AppContentParam(
        TenantId tenantId,
        long id,
        long appId,
        string code,
        string kind,
        string configJson)
        : base(tenantId)
    {
        Id = id;
        AppId = appId;
        Code = code;
        Kind = kind;
        ConfigJson = string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public long AppId { get; private set; }

    /// <summary>内容参数编码（应用内唯一）。</summary>
    [SugarColumn(Length = 128, IsNullable = false)]
    public string Code { get; private set; }

    /// <summary>类型（6 类）：text / image / data / link / media / ai（docx §U11）。</summary>
    [SugarColumn(Length = 32, IsNullable = false)]
    public string Kind { get; private set; }

    /// <summary>配置 JSON（按 Kind 不同含不同字段，由前端 zod 校验）。</summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = false)]
    public string ConfigJson { get; private set; }

    /// <summary>描述 / 备注。</summary>
    [SugarColumn(Length = 1000, IsNullable = true)]
    public string? Description { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(string kind, string configJson, string? description)
    {
        Kind = kind;
        ConfigJson = string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson;
        Description = description;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
