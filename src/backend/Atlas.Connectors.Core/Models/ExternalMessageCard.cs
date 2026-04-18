namespace Atlas.Connectors.Core.Models;

/// <summary>
/// 跨 provider 的消息卡片抽象。
/// 上层只需提供语义字段（标题 / 正文 / 状态色 / 主按钮），由 provider 实现转成各家专有格式
/// （企微 template_card / 飞书 interactive card）。
/// </summary>
public sealed record ExternalMessageCard
{
    public required string Title { get; init; }

    public string? Subtitle { get; init; }

    public string? Content { get; init; }

    /// <summary>状态色 / 标签色，建议使用语义值（"info" / "success" / "warning" / "danger" / "default"）。</summary>
    public string Tone { get; init; } = "info";

    public string? JumpUrl { get; init; }

    public IReadOnlyList<ExternalMessageCardField>? Fields { get; init; }

    public IReadOnlyList<ExternalMessageCardAction>? Actions { get; init; }

    /// <summary>
    /// 卡片版本号，用于 provider 在更新时做幂等判断（企微 update_template_card 的 response_code 流程也基于此）。
    /// </summary>
    public int CardVersion { get; init; } = 1;

    /// <summary>
    /// 业务关联键，便于服务端找到 ExternalMessageDispatch 记录后再发起 update_template_card。
    /// </summary>
    public string? BusinessKey { get; init; }
}

public sealed record ExternalMessageCardField
{
    public required string Key { get; init; }

    public required string Value { get; init; }

    public bool Highlight { get; init; }
}

public sealed record ExternalMessageCardAction
{
    public required string Key { get; init; }

    public required string Text { get; init; }

    public string Style { get; init; } = "default";

    public string? JumpUrl { get; init; }
}
