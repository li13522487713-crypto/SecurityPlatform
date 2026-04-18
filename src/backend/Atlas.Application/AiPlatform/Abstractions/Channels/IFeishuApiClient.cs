using System.Threading;
using System.Threading.Tasks;

namespace Atlas.Application.AiPlatform.Abstractions.Channels;

/// <summary>
/// 治理 M-G02-C6：飞书开放平台 API 客户端抽象。
/// 负责 tenant_access_token 缓存与刷新、消息发送（im messages）。
/// 使用 IHttpClientFactory 命名客户端，禁止 new HttpClient。
/// </summary>
public interface IFeishuApiClient
{
    /// <summary>
    /// 获取（缓存命中或重新拉取）tenant_access_token。
    /// 实现：内存级 SemaphoreSlim 防止刷新风暴；token 提前 60 秒过期触发刷新；
    /// 写库更新 <c>FeishuChannelCredential.TenantAccessTokenExpiresAt</c> 与 <c>RefreshCount</c>。
    /// </summary>
    Task<string> GetTenantAccessTokenAsync(
        long channelId,
        string appId,
        string appSecret,
        CancellationToken cancellationToken);

    /// <summary>
    /// 发送 IM 消息：POST /open-apis/im/v1/messages?receive_id_type={receiveType}
    /// receive_id_type ∈ open_id / user_id / chat_id / email
    /// 失败时抛 <c>BusinessException("FEISHU_API_FAILED", message)</c>，调用方决定是否重试。
    /// </summary>
    Task SendImMessageAsync(
        string tenantAccessToken,
        string receiveIdType,
        string receiveId,
        string msgType,
        string content,
        CancellationToken cancellationToken);
}
