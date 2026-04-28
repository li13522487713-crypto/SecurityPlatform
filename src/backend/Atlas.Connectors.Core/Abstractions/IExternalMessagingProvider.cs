using Atlas.Connectors.Core.Models;

namespace Atlas.Connectors.Core.Abstractions;

/// <summary>
/// 外部消息 Provider：纯文本 / 富文本 / 卡片 + 卡片更新。
/// 卡片更新结果（response_code / message_id / card_version）必须返回，便于上层落 ExternalMessageDispatch。
/// </summary>
public interface IExternalMessagingProvider
{
    string ProviderType { get; }

    Task<ExternalMessageDispatchResult> SendTextAsync(ConnectorContext context, ExternalMessageRecipient recipient, string text, CancellationToken cancellationToken);

    Task<ExternalMessageDispatchResult> SendCardAsync(ConnectorContext context, ExternalMessageRecipient recipient, ExternalMessageCard card, CancellationToken cancellationToken);

    /// <summary>
    /// 更新已发送的模板卡片消息（企微 update_template_card / 飞书 patch interactive card）。
    /// 调用方需提供首发时返回的 ExternalMessageDispatchResult 关键字段。
    /// </summary>
    Task<ExternalMessageDispatchResult> UpdateCardAsync(ConnectorContext context, ExternalMessageDispatchResult previous, ExternalMessageCard card, CancellationToken cancellationToken);
}

public sealed record ExternalMessageRecipient
{
    public IReadOnlyList<string>? UserIds { get; init; }

    public IReadOnlyList<string>? DepartmentIds { get; init; }

    public IReadOnlyList<string>? ChatIds { get; init; }

    /// <summary>是否广播给应用全员（仅企微/飞书部分场景）。</summary>
    public bool ToAll { get; init; }
}

public sealed record ExternalMessageDispatchResult
{
    public required string ProviderType { get; init; }

    public required string MessageId { get; init; }

    /// <summary>用于卡片更新的回执码（企微 response_code）。</summary>
    public string? ResponseCode { get; init; }

    public int CardVersion { get; init; }

    public DateTimeOffset SentAt { get; init; } = DateTimeOffset.UtcNow;

    public string? RawJson { get; init; }
}
