using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.AiPlatform.Entities;

/// <summary>
/// 工作空间渠道发布记录（M-G02-C2 治理 §3 S1）。
///
/// 一次「发布」= 把工作空间下某个 <see cref="WorkspacePublishChannel"/> 与
/// <see cref="AgentPublication"/>（或裸 Agent）的快照绑定，并由
/// IWorkspaceChannelConnector 真实下发到外部渠道（Web SDK / 飞书 / 微信 …）。
///
/// 设计要点：
/// - 与 <see cref="WorkspacePublishChannel"/> 1：N，可配合 <see cref="AgentPublication"/>
///   做版本回滚；当多次发布时旧记录会被标记为 <c>superseded</c>，最新成功记录为 <c>active</c>。
/// - 不存敏感凭据：connector 真实下发后只把可对外暴露的元数据写到
///   <see cref="PublicMetadataJson"/>（snippet / endpoint / appId 等）。
/// - <see cref="ConfigSnapshotJson"/> 为发布瞬间的渠道完整配置快照，回滚 / 重新发布时按它再次执行。
/// </summary>
[SugarTable("WorkspaceChannelRelease")]
public sealed class WorkspaceChannelRelease : TenantEntity
{
    public const string StatusPending = "pending";
    public const string StatusActive = "active";
    public const string StatusSuperseded = "superseded";
    public const string StatusRolledBack = "rolled-back";
    public const string StatusFailed = "failed";

    public WorkspaceChannelRelease()
        : base(TenantId.Empty)
    {
        WorkspaceId = string.Empty;
        Status = StatusPending;
        PublicMetadataJson = "{}";
        ConfigSnapshotJson = "{}";
        ReleaseNote = string.Empty;
        ConnectorMessage = string.Empty;
        CreatedAt = DateTime.UtcNow;
        ReleasedAt = CreatedAt;
    }

    public WorkspaceChannelRelease(
        TenantId tenantId,
        string workspaceId,
        long channelId,
        long? agentId,
        long? agentPublicationId,
        int releaseNo,
        string configSnapshotJson,
        string? releaseNote,
        long releasedByUserId,
        long id)
        : base(tenantId)
    {
        Id = id;
        WorkspaceId = workspaceId;
        ChannelId = channelId;
        AgentId = agentId;
        AgentPublicationId = agentPublicationId;
        ReleaseNo = releaseNo;
        Status = StatusPending;
        PublicMetadataJson = "{}";
        ConfigSnapshotJson = string.IsNullOrWhiteSpace(configSnapshotJson) ? "{}" : configSnapshotJson;
        ReleaseNote = releaseNote ?? string.Empty;
        ConnectorMessage = string.Empty;
        ReleasedByUserId = releasedByUserId;
        CreatedAt = DateTime.UtcNow;
        ReleasedAt = CreatedAt;
    }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string WorkspaceId { get; private set; }

    public long ChannelId { get; private set; }

    [SugarColumn(IsNullable = true)]
    public long? AgentId { get; private set; }

    [SugarColumn(IsNullable = true)]
    public long? AgentPublicationId { get; private set; }

    /// <summary>同 channel 内单调递增的发布序号，从 1 开始。</summary>
    public int ReleaseNo { get; private set; }

    /// <summary>pending / active / superseded / rolled-back / failed。</summary>
    [SugarColumn(Length = 16, IsNullable = false)]
    public string Status { get; private set; }

    /// <summary>connector 返回的可对外暴露元数据（snippet / endpoint catalog / external bot id）。</summary>
    [SugarColumn(ColumnDataType = "TEXT", IsNullable = false)]
    public string PublicMetadataJson { get; private set; }

    /// <summary>发布时的渠道完整配置快照，回滚时按它再次执行。</summary>
    [SugarColumn(ColumnDataType = "TEXT", IsNullable = false)]
    public string ConfigSnapshotJson { get; private set; }

    [SugarColumn(Length = 512, IsNullable = false)]
    public string ReleaseNote { get; private set; }

    /// <summary>connector 最近一次执行的提示或错误信息（成功 / 失败均会写）。</summary>
    [SugarColumn(Length = 1024, IsNullable = false)]
    public string ConnectorMessage { get; private set; }

    public long ReleasedByUserId { get; private set; }

    public DateTime ReleasedAt { get; private set; }

    public DateTime CreatedAt { get; private set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? SupersededAt { get; private set; }

    /// <summary>若本次发布是回滚到某个历史发布，记录其 ReleaseId（自身 ReleaseNo 仍单调递增）。</summary>
    [SugarColumn(IsNullable = true)]
    public long? RolledBackFromReleaseId { get; private set; }

    public void MarkActive(string publicMetadataJson, string? connectorMessage)
    {
        Status = StatusActive;
        PublicMetadataJson = string.IsNullOrWhiteSpace(publicMetadataJson) ? "{}" : publicMetadataJson;
        ConnectorMessage = connectorMessage ?? string.Empty;
        ReleasedAt = DateTime.UtcNow;
        SupersededAt = null;
    }

    public void MarkFailed(string connectorMessage)
    {
        Status = StatusFailed;
        ConnectorMessage = connectorMessage ?? string.Empty;
        ReleasedAt = DateTime.UtcNow;
    }

    public void MarkSuperseded()
    {
        if (Status == StatusActive)
        {
            Status = StatusSuperseded;
            SupersededAt = DateTime.UtcNow;
        }
    }

    public void MarkRolledBack(string connectorMessage)
    {
        Status = StatusRolledBack;
        ConnectorMessage = connectorMessage ?? string.Empty;
        SupersededAt = DateTime.UtcNow;
    }

    public void AttachRollbackOrigin(long sourceReleaseId)
    {
        RolledBackFromReleaseId = sourceReleaseId;
    }
}
