using System.Globalization;
using System.Text.Json;
using Atlas.Connectors.Core;
using Atlas.Connectors.Core.Abstractions;
using Atlas.Connectors.Core.Models;
using Atlas.Connectors.DingTalk.Internal;

namespace Atlas.Connectors.DingTalk;

/// <summary>
/// 钉钉消息 Provider：
/// - 工作通知：v1 /topapi/message/corpconversation/asyncsend_v2（文本、ActionCard）；
/// - 工作通知撤回：v1 /topapi/message/corpconversation/recall（替代 update card 的最小语义）。
/// 钉钉工作通知没有"卡片就地更新"接口，UpdateCardAsync 通过 recall + 重新发送实现。
/// </summary>
public sealed class DingTalkMessagingProvider : IExternalMessagingProvider
{
    private readonly DingTalkApiClient _api;

    public DingTalkMessagingProvider(DingTalkApiClient api)
    {
        _api = api;
    }

    public string ProviderType => DingTalkConnectorMarker.ProviderType;

    public async Task<ExternalMessageDispatchResult> SendTextAsync(ConnectorContext context, ExternalMessageRecipient recipient, string text, CancellationToken cancellationToken)
    {
        var runtime = await _api.ResolveRuntimeOptionsAsync(context, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(runtime.AgentId))
        {
            throw new ConnectorException(ConnectorErrorCodes.MessagingFailed, "DingTalk asyncsend_v2 requires AgentId.", ProviderType);
        }

        var body = new
        {
            agent_id = long.Parse(runtime.AgentId, CultureInfo.InvariantCulture),
            userid_list = recipient.UserIds is { Count: > 0 } ? string.Join(',', recipient.UserIds) : null,
            dept_id_list = recipient.DepartmentIds is { Count: > 0 } ? string.Join(',', recipient.DepartmentIds) : null,
            to_all_user = recipient.ToAll,
            msg = new
            {
                msgtype = "text",
                text = new { content = text },
            },
        };
        var resp = await _api.SendLegacyPostJsonAsync<object, DingTalkSendWorkNoticeResponse>(context, "/topapi/message/corpconversation/asyncsend_v2", body, cancellationToken).ConfigureAwait(false);

        return new ExternalMessageDispatchResult
        {
            ProviderType = ProviderType,
            MessageId = resp.TaskId.ToString(CultureInfo.InvariantCulture),
            ResponseCode = null,
            CardVersion = 1,
            RawJson = JsonSerializer.Serialize(resp),
        };
    }

    public async Task<ExternalMessageDispatchResult> SendCardAsync(ConnectorContext context, ExternalMessageRecipient recipient, ExternalMessageCard card, CancellationToken cancellationToken)
    {
        var runtime = await _api.ResolveRuntimeOptionsAsync(context, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(runtime.AgentId))
        {
            throw new ConnectorException(ConnectorErrorCodes.MessagingFailed, "DingTalk asyncsend_v2 requires AgentId.", ProviderType);
        }

        var actionCard = BuildActionCard(card);
        var body = new
        {
            agent_id = long.Parse(runtime.AgentId, CultureInfo.InvariantCulture),
            userid_list = recipient.UserIds is { Count: > 0 } ? string.Join(',', recipient.UserIds) : null,
            dept_id_list = recipient.DepartmentIds is { Count: > 0 } ? string.Join(',', recipient.DepartmentIds) : null,
            to_all_user = recipient.ToAll,
            msg = new
            {
                msgtype = "action_card",
                action_card = actionCard,
            },
        };
        var resp = await _api.SendLegacyPostJsonAsync<object, DingTalkSendWorkNoticeResponse>(context, "/topapi/message/corpconversation/asyncsend_v2", body, cancellationToken).ConfigureAwait(false);

        return new ExternalMessageDispatchResult
        {
            ProviderType = ProviderType,
            MessageId = resp.TaskId.ToString(CultureInfo.InvariantCulture),
            ResponseCode = null,
            CardVersion = card.CardVersion,
            RawJson = JsonSerializer.Serialize(resp),
        };
    }

    public async Task<ExternalMessageDispatchResult> UpdateCardAsync(ConnectorContext context, ExternalMessageDispatchResult previous, ExternalMessageCard card, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(previous);
        var runtime = await _api.ResolveRuntimeOptionsAsync(context, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(runtime.AgentId))
        {
            throw new ConnectorException(ConnectorErrorCodes.MessagingFailed, "DingTalk recall requires AgentId.", ProviderType);
        }

        // 钉钉工作通知不支持卡片就地更新；按"撤回 + 重发"实现 update 语义。
        if (long.TryParse(previous.MessageId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var taskId))
        {
            var recallBody = new
            {
                agent_id = long.Parse(runtime.AgentId, CultureInfo.InvariantCulture),
                msg_task_id = taskId,
            };
            try
            {
                await _api.SendLegacyPostJsonAsync<object, DingTalkLegacyResponse>(context, "/topapi/message/corpconversation/recall", recallBody, cancellationToken).ConfigureAwait(false);
            }
            catch (ConnectorException)
            {
                // 撤回失败不阻塞重发；钉钉撤回有 24h 时效，超过会失败。
            }
        }

        // 重发卡片。复用 recipient 信息：previous 没带 recipient，使用 card.JumpUrl 推断或使用 ToAll。
        var resendRecipient = new ExternalMessageRecipient { ToAll = true };
        return await SendCardAsync(context, resendRecipient, card, cancellationToken).ConfigureAwait(false);
    }

    private static object BuildActionCard(ExternalMessageCard card)
    {
        return new
        {
            title = card.Title,
            markdown = string.Concat(
                "### ", card.Title, "\n\n",
                string.IsNullOrEmpty(card.Subtitle) ? string.Empty : ("**" + card.Subtitle + "**\n\n"),
                card.Content ?? string.Empty,
                card.Fields is null || card.Fields.Count == 0
                    ? string.Empty
                    : "\n\n" + string.Join('\n', card.Fields.Select(f => $"- **{f.Key}**：{f.Value}"))),
            single_title = string.IsNullOrEmpty(card.JumpUrl) ? null : card.Actions?.FirstOrDefault()?.Text ?? "查看详情",
            single_url = card.JumpUrl,
        };
    }
}
