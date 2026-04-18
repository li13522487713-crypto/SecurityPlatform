using System.Collections.Generic;

namespace Atlas.Application.AiPlatform.Abstractions.Channels;

/// <summary>
/// 渠道适配器注册中心：按 ChannelType 字符串解析具体 IWorkspaceChannelConnector。
/// 由 DI 在启动期把所有 IWorkspaceChannelConnector 实现自动收集。
/// </summary>
public interface IWorkspaceChannelConnectorRegistry
{
    /// <summary>已注册的所有渠道类型标识（去重，按字典序）。</summary>
    IReadOnlyList<string> SupportedChannelTypes { get; }

    /// <summary>按 ChannelType 解析对应的 connector。返回 null 表示当前部署未启用该渠道。</summary>
    IWorkspaceChannelConnector? Resolve(string channelType);

    /// <summary>按 ChannelType 解析；未注册时抛 BusinessException("CHANNEL_TYPE_NOT_SUPPORTED")。</summary>
    IWorkspaceChannelConnector RequireResolve(string channelType);
}
