using System.Threading;
using System.Threading.Tasks;

namespace Atlas.Application.AiPlatform.Abstractions.Channels;

/// <summary>
/// 治理 M-G02-C10：微信公众号开放接口客户端。
/// 负责 access_token 缓存与刷新，以及客服消息发送（cgi-bin/message/custom/send）。
/// 使用 IHttpClientFactory 命名客户端，禁止 new HttpClient。
/// </summary>
public interface IWechatMpApiClient
{
    /// <summary>
    /// 获取（缓存命中或重新拉取）access_token。
    /// 实现细节同 Feishu：内存缓存 + per-channel SemaphoreSlim + 提前 60 秒过期触发刷新；
    /// 写库更新 <c>WechatMpChannelCredential.AccessTokenExpiresAt + RefreshCount</c>。
    /// </summary>
    Task<string> GetAccessTokenAsync(
        long channelId,
        string appId,
        string appSecret,
        CancellationToken cancellationToken);

    /// <summary>
    /// 发送客服消息（POST /cgi-bin/message/custom/send?access_token=...）
    /// errcode != 0 时抛 <c>BusinessException("WECHAT_MP_API_FAILED", message)</c>。
    /// </summary>
    Task SendCustomerMessageAsync(
        string accessToken,
        string toUser,
        string msgType,
        string content,
        CancellationToken cancellationToken);
}
