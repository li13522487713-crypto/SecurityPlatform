using System.Text.Json;
using Atlas.Connectors.Core;
using Atlas.Connectors.Core.Abstractions;
using Atlas.Connectors.Core.Models;
using Atlas.Connectors.WeCom.Internal;

namespace Atlas.Connectors.WeCom;

/// <summary>
/// 企业微信消息 Provider：message/send 文本/图文/文件 + 模板卡片 + update_template_card。
/// </summary>
public sealed class WeComMessagingProvider : IExternalMessagingProvider
{
    private readonly WeComApiClient _api;

    public WeComMessagingProvider(WeComApiClient api)
    {
        _api = api;
    }

    public string ProviderType => WeComConnectorMarker.ProviderType;

    public async Task<ExternalMessageDispatchResult> SendTextAsync(ConnectorContext context, ExternalMessageRecipient recipient, string text, CancellationToken cancellationToken)
    {
        var runtime = await _api.ResolveRuntimeOptionsAsync(context, cancellationToken).ConfigureAwait(false);
        var body = new
        {
            touser = recipient.UserIds is { Count: > 0 } ? string.Join('|', recipient.UserIds) : (recipient.ToAll ? "@all" : ""),
            toparty = recipient.DepartmentIds is { Count: > 0 } ? string.Join('|', recipient.DepartmentIds) : "",
            msgtype = "text",
            agentid = int.Parse(runtime.AgentId, System.Globalization.CultureInfo.InvariantCulture),
            text = new { content = text },
        };
        var resp = await _api.SendAuthorizedPostJsonAsync<object, WeComMessageSendResponse>(context, "/cgi-bin/message/send", body, null, cancellationToken).ConfigureAwait(false);
        return new ExternalMessageDispatchResult
        {
            ProviderType = ProviderType,
            MessageId = resp.MsgId ?? Guid.NewGuid().ToString("N"),
            ResponseCode = resp.ResponseCode,
            CardVersion = 1,
            RawJson = JsonSerializer.Serialize(resp),
        };
    }

    public async Task<ExternalMessageDispatchResult> SendCardAsync(ConnectorContext context, ExternalMessageRecipient recipient, ExternalMessageCard card, CancellationToken cancellationToken)
    {
        var runtime = await _api.ResolveRuntimeOptionsAsync(context, cancellationToken).ConfigureAwait(false);
        var templateCard = BuildTemplateCard(card);
        var body = new
        {
            touser = recipient.UserIds is { Count: > 0 } ? string.Join('|', recipient.UserIds) : (recipient.ToAll ? "@all" : ""),
            toparty = recipient.DepartmentIds is { Count: > 0 } ? string.Join('|', recipient.DepartmentIds) : "",
            msgtype = "template_card",
            agentid = int.Parse(runtime.AgentId, System.Globalization.CultureInfo.InvariantCulture),
            template_card = templateCard,
        };
        var resp = await _api.SendAuthorizedPostJsonAsync<object, WeComMessageSendResponse>(context, "/cgi-bin/message/send", body, null, cancellationToken).ConfigureAwait(false);
        return new ExternalMessageDispatchResult
        {
            ProviderType = ProviderType,
            MessageId = resp.MsgId ?? Guid.NewGuid().ToString("N"),
            ResponseCode = resp.ResponseCode,
            CardVersion = card.CardVersion,
            RawJson = JsonSerializer.Serialize(resp),
        };
    }

    public async Task<ExternalMessageDispatchResult> UpdateCardAsync(ConnectorContext context, ExternalMessageDispatchResult previous, ExternalMessageCard card, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(previous);
        var runtime = await _api.ResolveRuntimeOptionsAsync(context, cancellationToken).ConfigureAwait(false);
        var body = new
        {
            userids = Array.Empty<string>(),
            partyids = Array.Empty<string>(),
            tagids = Array.Empty<string>(),
            atall = 0,
            agentid = int.Parse(runtime.AgentId, System.Globalization.CultureInfo.InvariantCulture),
            response_code = previous.ResponseCode,
            button = new { replace_name = card.Actions?.FirstOrDefault()?.Text ?? "已处理" },
            template_card = BuildTemplateCard(card),
        };
        var resp = await _api.SendAuthorizedPostJsonAsync<object, WeComMessageSendResponse>(context, "/cgi-bin/message/update_template_card", body, null, cancellationToken).ConfigureAwait(false);
        return previous with
        {
            ResponseCode = resp.ResponseCode ?? previous.ResponseCode,
            CardVersion = card.CardVersion,
            SentAt = DateTimeOffset.UtcNow,
            RawJson = JsonSerializer.Serialize(resp),
        };
    }

    private static object BuildTemplateCard(ExternalMessageCard card)
    {
        // 简化版 text_notice 模板卡片，便于通用性
        return new
        {
            card_type = "text_notice",
            source = (object?)null,
            main_title = new { title = card.Title, desc = card.Subtitle },
            sub_title_text = card.Content,
            horizontal_content_list = card.Fields?.Select(f => new { keyname = f.Key, value = f.Value }).ToArray(),
            jump_list = string.IsNullOrEmpty(card.JumpUrl) ? null : new[] { new { type = 1, url = card.JumpUrl, title = "查看详情" } },
            card_action = string.IsNullOrEmpty(card.JumpUrl) ? null : new { type = 1, url = card.JumpUrl },
        };
    }
}

internal sealed class WeComMessageSendResponse : WeComApiResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("msgid")]
    public string? MsgId { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("response_code")]
    public string? ResponseCode { get; set; }
}
