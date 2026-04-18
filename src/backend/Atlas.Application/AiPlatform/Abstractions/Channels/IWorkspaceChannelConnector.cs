using System.Threading;
using System.Threading.Tasks;

namespace Atlas.Application.AiPlatform.Abstractions.Channels;

/// <summary>
/// 工作空间发布渠道适配器（Web SDK / Open API / 飞书 / 微信公众号 等）。
/// 每种 ChannelType 对应一个 IWorkspaceChannelConnector 实现，由
/// IWorkspaceChannelConnectorRegistry 按 ChannelType 解析。
///
/// 实现方应保证：
/// - 所有外部调用（HttpClient）来自 IHttpClientFactory，禁止直接 new HttpClient；
/// - 凭据落库走 LowCodeCredentialProtector；
/// - 异常以 Atlas.Core.Errors.BusinessException 表达业务语义；
/// - 真实接通，不允许桩响应（参考治理计划 §10「零占位审计」）。
/// </summary>
public interface IWorkspaceChannelConnector
{
    /// <summary>
    /// 渠道类型标识，与 WorkspacePublishChannel.ChannelType 字符串保持一致
    /// （例如 web-sdk / open-api / feishu / wechat-mp）。
    /// </summary>
    string ChannelType { get; }

    /// <summary>
    /// 创建/更新一次渠道发布；实现需要：1) 真实生成可用凭据/snippet/endpoint
    /// 2) 写入 WorkspaceChannelRelease 由调用方完成；本方法只返回执行结果与可对外的元数据。
    /// </summary>
    Task<ChannelPublishResult> PublishAsync(ChannelPublishContext context, CancellationToken cancellationToken);

    /// <summary>
    /// 处理渠道入站事件（webhook 调用）；实现需要完成签名校验、消息解码，
    /// 然后通过 Atlas 内部 dispatch 路径触发 Agent 对话。
    /// </summary>
    Task<ChannelDispatchResult> HandleInboundAsync(ChannelInboundContext context, CancellationToken cancellationToken);

    /// <summary>
    /// 主动向渠道发送出站消息（例如 Agent 对话回包、卡片推送）；实现需要负责
    /// 凭据刷新、限流退避、统一审计写入。
    /// </summary>
    Task SendOutboundAsync(ChannelOutboundContext context, CancellationToken cancellationToken);
}
