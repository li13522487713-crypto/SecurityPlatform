using System.Text.Json;
using Atlas.Connectors.Core;
using Atlas.Connectors.Core.Abstractions;
using Atlas.Connectors.Core.Models;

namespace Atlas.Connectors.Feishu;

/// <summary>
/// 飞书消息 Provider：im/v1/messages 文本/卡片 + im/v1/messages/{id} PATCH 卡片更新。
/// </summary>
public sealed class FeishuMessagingProvider : IExternalMessagingProvider
{
    private readonly FeishuApiClient _api;

    public FeishuMessagingProvider(FeishuApiClient api)
    {
        _api = api;
    }

    public string ProviderType => FeishuConnectorMarker.ProviderType;

    public async Task<ExternalMessageDispatchResult> SendTextAsync(ConnectorContext context, ExternalMessageRecipient recipient, string text, CancellationToken cancellationToken)
    {
        var receiver = ResolveReceiver(recipient, out var receiverIdType);
        var body = new
        {
            receive_id = receiver,
            content = JsonSerializer.Serialize(new { text }),
            msg_type = "text",
        };
        var path = $"/open-apis/im/v1/messages?receive_id_type={receiverIdType}";
        var resp = await _api.SendTenantPostAsync<object, FeishuMessageData>(context, path, body, cancellationToken).ConfigureAwait(false);
        return new ExternalMessageDispatchResult
        {
            ProviderType = ProviderType,
            MessageId = resp.Data?.MessageId ?? Guid.NewGuid().ToString("N"),
            CardVersion = 1,
            RawJson = JsonSerializer.Serialize(resp),
        };
    }

    public async Task<ExternalMessageDispatchResult> SendCardAsync(ConnectorContext context, ExternalMessageRecipient recipient, ExternalMessageCard card, CancellationToken cancellationToken)
    {
        var receiver = ResolveReceiver(recipient, out var receiverIdType);
        var body = new
        {
            receive_id = receiver,
            content = JsonSerializer.Serialize(BuildInteractiveCard(card)),
            msg_type = "interactive",
        };
        var path = $"/open-apis/im/v1/messages?receive_id_type={receiverIdType}";
        var resp = await _api.SendTenantPostAsync<object, FeishuMessageData>(context, path, body, cancellationToken).ConfigureAwait(false);
        return new ExternalMessageDispatchResult
        {
            ProviderType = ProviderType,
            MessageId = resp.Data?.MessageId ?? Guid.NewGuid().ToString("N"),
            CardVersion = card.CardVersion,
            RawJson = JsonSerializer.Serialize(resp),
        };
    }

    public async Task<ExternalMessageDispatchResult> UpdateCardAsync(ConnectorContext context, ExternalMessageDispatchResult previous, ExternalMessageCard card, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(previous);
        var body = new
        {
            content = JsonSerializer.Serialize(BuildInteractiveCard(card)),
        };
        var path = $"/open-apis/im/v1/messages/{Uri.EscapeDataString(previous.MessageId)}";
        var resp = await _api.SendTenantPatchAsync<object, FeishuMessageData>(context, path, body, cancellationToken).ConfigureAwait(false);
        return previous with
        {
            CardVersion = card.CardVersion,
            SentAt = DateTimeOffset.UtcNow,
            RawJson = JsonSerializer.Serialize(resp),
        };
    }

    private static string ResolveReceiver(ExternalMessageRecipient recipient, out string receiverIdType)
    {
        if (recipient.UserIds is { Count: > 0 } users)
        {
            receiverIdType = "open_id";
            return users[0];
        }
        if (recipient.ChatIds is { Count: > 0 } chats)
        {
            receiverIdType = "chat_id";
            return chats[0];
        }
        receiverIdType = "open_id";
        return string.Empty;
    }

    private static object BuildInteractiveCard(ExternalMessageCard card)
    {
        return new
        {
            config = new { wide_screen_mode = true },
            header = new
            {
                title = new { tag = "plain_text", content = card.Title },
                template = ResolveTone(card.Tone),
            },
            elements = BuildElements(card),
        };
    }

    private static IEnumerable<object> BuildElements(ExternalMessageCard card)
    {
        if (!string.IsNullOrEmpty(card.Subtitle))
        {
            yield return new { tag = "div", text = new { tag = "plain_text", content = card.Subtitle } };
        }
        if (!string.IsNullOrEmpty(card.Content))
        {
            yield return new { tag = "div", text = new { tag = "lark_md", content = card.Content } };
        }
        if (card.Fields is { Count: > 0 })
        {
            yield return new
            {
                tag = "div",
                fields = card.Fields.Select(f => new { is_short = !f.Highlight, text = new { tag = "lark_md", content = $"**{f.Key}**: {f.Value}" } }).ToArray(),
            };
        }
        if (card.Actions is { Count: > 0 })
        {
            yield return new
            {
                tag = "action",
                actions = card.Actions.Select(a => new
                {
                    tag = "button",
                    text = new { tag = "plain_text", content = a.Text },
                    type = a.Style == "danger" ? "danger" : (a.Style == "primary" ? "primary" : "default"),
                    url = a.JumpUrl,
                    value = new { key = a.Key },
                }).ToArray(),
            };
        }
    }

    private static string ResolveTone(string tone) => tone.ToLowerInvariant() switch
    {
        "success" => "green",
        "warning" => "yellow",
        "danger" => "red",
        "info" => "blue",
        _ => "wathet",
    };
}

internal sealed class FeishuMessageData
{
    [System.Text.Json.Serialization.JsonPropertyName("message_id")]
    public string? MessageId { get; set; }
}
