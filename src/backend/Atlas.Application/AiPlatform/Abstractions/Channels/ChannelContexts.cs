using System.Collections.Generic;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions.Channels;

/// <summary>
/// 渠道发布上下文：由 IWorkspaceChannelReleaseService（M-G02-C2）在创建发布时构造，
/// 然后交给具体 IWorkspaceChannelConnector 完成真实接通（M-G02-C3 起）。
/// </summary>
public sealed record ChannelPublishContext(
    TenantId TenantId,
    long WorkspaceId,
    long ChannelId,
    string ChannelType,
    long? AgentId,
    long? AgentPublicationId,
    int Version,
    long ReleasedByUserId,
    string ConfigSnapshotJson);

/// <summary>
/// 渠道发布执行结果：由具体 connector 返回，包含可对外暴露的元数据
/// （例如 Web SDK 的 snippet、Open API 的 endpoint 列表）。
/// </summary>
public sealed record ChannelPublishResult(
    bool Success,
    string Status,
    string? PublicMetadataJson,
    string? FailureReason);

/// <summary>
/// 入站事件上下文：来自渠道 webhook（M-G02-C7 / C11 等）。
/// </summary>
public sealed record ChannelInboundContext(
    TenantId TenantId,
    long ChannelId,
    string ChannelType,
    string EventType,
    string? ExternalUserId,
    string? Conversation,
    string PayloadJson,
    IReadOnlyDictionary<string, string> Headers);

/// <summary>
/// 渠道入站分发结果：connector 对外回包之前必须先完成 Agent 对话；
/// Result.Handled = false 表示该消息未被 dispatch 到任何 Agent。
/// </summary>
public sealed record ChannelDispatchResult(
    bool Handled,
    string? AgentResponseJson,
    string? FailureReason);

/// <summary>
/// 出站消息上下文：对话/卡片/事件由 connector 调用对应渠道的 IM/客服 API 发送。
/// </summary>
public sealed record ChannelOutboundContext(
    TenantId TenantId,
    long ChannelId,
    string ChannelType,
    string ExternalUserId,
    string? Conversation,
    string MessageType,
    string MessagePayloadJson);
