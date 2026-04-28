namespace Atlas.Connectors.Core.Models;

/// <summary>
/// 外部审批模板（企微 getapprovaltmp / 飞书 approval/v4/approvals/{code}）的统一抽象。
/// </summary>
public sealed record ExternalApprovalTemplate
{
    public required string ProviderType { get; init; }

    public required string ProviderTenantId { get; init; }

    /// <summary>外部模板唯一标识（企微 template_id / 飞书 approval_code）。</summary>
    public required string ExternalTemplateId { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>模板控件列表（顺序与 provider 返回保持一致，便于前端展示）。</summary>
    public required IReadOnlyList<ExternalApprovalTemplateControl> Controls { get; init; }

    public string? RawJson { get; init; }
}

/// <summary>
/// 单个模板控件元数据。控件类型采用统一字符串以避免硬编码 provider 枚举值。
/// </summary>
public sealed record ExternalApprovalTemplateControl
{
    public required string ControlId { get; init; }

    public required string ControlType { get; init; }

    public required string Title { get; init; }

    public bool Required { get; init; }

    /// <summary>选项类控件（Selector / Radio / Checkbox）的可选值列表。</summary>
    public IReadOnlyList<ExternalApprovalTemplateOption>? Options { get; init; }

    /// <summary>provider 私有元数据，按需透传。</summary>
    public IReadOnlyDictionary<string, string>? Extra { get; init; }
}

public sealed record ExternalApprovalTemplateOption
{
    public required string Key { get; init; }

    public required string Text { get; init; }
}
