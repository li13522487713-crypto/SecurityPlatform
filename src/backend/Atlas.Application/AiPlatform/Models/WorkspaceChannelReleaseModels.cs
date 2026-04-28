using System.ComponentModel.DataAnnotations;

namespace Atlas.Application.AiPlatform.Models;

/// <summary>
/// 渠道发布记录 DTO（M-G02-C2）。
/// </summary>
public sealed record WorkspaceChannelReleaseDto(
    string Id,
    string WorkspaceId,
    string ChannelId,
    string? AgentId,
    string? AgentPublicationId,
    int ReleaseNo,
    string Status,
    string? PublicMetadataJson,
    string? ReleaseNote,
    string? ConnectorMessage,
    string? RolledBackFromReleaseId,
    long ReleasedByUserId,
    DateTimeOffset ReleasedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SupersededAt);

/// <summary>
/// 创建发布请求：可选指定要发布的 Agent 与历史 publication；
/// 如果未传 publicationId，则按 agentId 取最近一次活动 publication。
/// </summary>
public sealed record WorkspaceChannelReleaseCreateRequest(
    string? AgentId,
    string? AgentPublicationId,
    [StringLength(512)] string? ReleaseNote);

/// <summary>
/// 回滚请求：把一个历史发布提升为当前活动发布；
/// 服务端会基于历史快照再次调用 connector，并写入新的发布记录。
/// </summary>
public sealed record WorkspaceChannelReleaseRollbackRequest(
    [Required] string TargetReleaseId,
    [StringLength(512)] string? ReleaseNote);
