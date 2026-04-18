using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions.Channels;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.Audit.Abstractions;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.Audit.Entities;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Repositories.AiPlatform;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.AiPlatform.Channels;

/// <summary>
/// 工作空间渠道发布编排服务。
///
/// 责任：
/// 1. 校验输入并按 (channelId, ReleaseNo+1) 落库；
/// 2. 解析 connector：拿到则真实下发，未注册的 channelType 一律记 <c>failed</c>；
/// 3. 多发布只允许一个 <c>active</c>：成功发布后把同一 channel 之前的 active 标 superseded；
/// 4. 回滚：以历史发布快照再发布一次，新记录 RolledBackFromReleaseId 指向源；
/// 5. 全程写审计（CHANNEL_RELEASE_PUBLISH / CHANNEL_RELEASE_ROLLBACK）。
/// </summary>
public sealed class WorkspaceChannelReleaseService : IWorkspaceChannelReleaseService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly WorkspaceChannelReleaseRepository _releaseRepository;
    private readonly WorkspacePublishChannelRepository _channelRepository;
    private readonly AgentPublicationRepository _agentPublicationRepository;
    private readonly IWorkspaceChannelConnectorRegistry _connectorRegistry;
    private readonly IIdGeneratorAccessor _idGenerator;
    private readonly IAuditWriter _auditWriter;
    private readonly ILogger<WorkspaceChannelReleaseService> _logger;

    public WorkspaceChannelReleaseService(
        WorkspaceChannelReleaseRepository releaseRepository,
        WorkspacePublishChannelRepository channelRepository,
        AgentPublicationRepository agentPublicationRepository,
        IWorkspaceChannelConnectorRegistry connectorRegistry,
        IIdGeneratorAccessor idGenerator,
        IAuditWriter auditWriter,
        ILogger<WorkspaceChannelReleaseService> logger)
    {
        _releaseRepository = releaseRepository;
        _channelRepository = channelRepository;
        _agentPublicationRepository = agentPublicationRepository;
        _connectorRegistry = connectorRegistry;
        _idGenerator = idGenerator;
        _auditWriter = auditWriter;
        _logger = logger;
    }

    public async Task<PagedResult<WorkspaceChannelReleaseDto>> ListAsync(
        TenantId tenantId,
        string workspaceId,
        string channelId,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken)
    {
        var channel = await LoadChannelOrThrowAsync(tenantId, workspaceId, channelId, cancellationToken);
        var pageIndex = Math.Max(1, pagedRequest.PageIndex);
        var pageSize = Math.Clamp(pagedRequest.PageSize, 1, 100);

        var (entities, total) = await _releaseRepository.SearchAsync(
            tenantId,
            workspaceId,
            channel.Id,
            pageIndex,
            pageSize,
            cancellationToken);

        return new PagedResult<WorkspaceChannelReleaseDto>(
            entities.Select(ToDto).ToArray(),
            total,
            pageIndex,
            pageSize);
    }

    public async Task<WorkspaceChannelReleaseDto> GetAsync(
        TenantId tenantId,
        string workspaceId,
        string channelId,
        string releaseId,
        CancellationToken cancellationToken)
    {
        var channel = await LoadChannelOrThrowAsync(tenantId, workspaceId, channelId, cancellationToken);
        var release = await LoadReleaseOrThrowAsync(tenantId, workspaceId, channel.Id, releaseId, cancellationToken);
        return ToDto(release);
    }

    public async Task<WorkspaceChannelReleaseDto> PublishAsync(
        TenantId tenantId,
        string workspaceId,
        string channelId,
        CurrentUserInfo currentUser,
        WorkspaceChannelReleaseCreateRequest request,
        CancellationToken cancellationToken)
    {
        var channel = await LoadChannelOrThrowAsync(tenantId, workspaceId, channelId, cancellationToken);

        long? agentId = ParseLongOrNull(request.AgentId);
        long? agentPublicationId = ParseLongOrNull(request.AgentPublicationId);

        if (agentPublicationId is not null)
        {
            var publication = await ResolvePublicationOrThrowAsync(tenantId, agentPublicationId.Value, cancellationToken);
            agentId ??= publication.AgentId;
        }

        var snapshotJson = BuildConfigSnapshotJson(channel, agentId, agentPublicationId, releaseNote: request.ReleaseNote, sourceReleaseId: null);

        return await PerformReleaseAsync(
            tenantId,
            channel,
            agentId,
            agentPublicationId,
            snapshotJson,
            request.ReleaseNote,
            currentUser,
            rolledBackFromReleaseId: null,
            auditAction: "CHANNEL_RELEASE_PUBLISH",
            cancellationToken);
    }

    public async Task<WorkspaceChannelReleaseDto> RollbackAsync(
        TenantId tenantId,
        string workspaceId,
        string channelId,
        CurrentUserInfo currentUser,
        WorkspaceChannelReleaseRollbackRequest request,
        CancellationToken cancellationToken)
    {
        var channel = await LoadChannelOrThrowAsync(tenantId, workspaceId, channelId, cancellationToken);
        var source = await LoadReleaseOrThrowAsync(tenantId, workspaceId, channel.Id, request.TargetReleaseId, cancellationToken);

        if (source.Status == WorkspaceChannelRelease.StatusActive)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "ChannelReleaseAlreadyActive");
        }

        // 回滚：使用源记录的快照重发
        var snapshotJson = string.IsNullOrWhiteSpace(source.ConfigSnapshotJson)
            ? BuildConfigSnapshotJson(channel, source.AgentId, source.AgentPublicationId, request.ReleaseNote, source.Id)
            : source.ConfigSnapshotJson;

        return await PerformReleaseAsync(
            tenantId,
            channel,
            source.AgentId,
            source.AgentPublicationId,
            snapshotJson,
            request.ReleaseNote ?? source.ReleaseNote,
            currentUser,
            rolledBackFromReleaseId: source.Id,
            auditAction: "CHANNEL_RELEASE_ROLLBACK",
            cancellationToken);
    }

    private async Task<WorkspaceChannelReleaseDto> PerformReleaseAsync(
        TenantId tenantId,
        WorkspacePublishChannel channel,
        long? agentId,
        long? agentPublicationId,
        string snapshotJson,
        string? releaseNote,
        CurrentUserInfo currentUser,
        long? rolledBackFromReleaseId,
        string auditAction,
        CancellationToken cancellationToken)
    {
        var nextNo = await _releaseRepository.GetMaxReleaseNoAsync(tenantId, channel.WorkspaceId, channel.Id, cancellationToken) + 1;
        var release = new WorkspaceChannelRelease(
            tenantId,
            channel.WorkspaceId,
            channel.Id,
            agentId,
            agentPublicationId,
            nextNo,
            snapshotJson,
            releaseNote,
            currentUser.UserId,
            _idGenerator.NextId());

        if (rolledBackFromReleaseId is not null)
        {
            release.AttachRollbackOrigin(rolledBackFromReleaseId.Value);
        }

        await _releaseRepository.AddAsync(release, cancellationToken);

        var connector = _connectorRegistry.Resolve(channel.ChannelType);
        if (connector is null)
        {
            var message = $"Channel connector for type '{channel.ChannelType}' is not registered in this deployment.";
            release.MarkFailed(message);
            await _releaseRepository.UpdateAsync(release, cancellationToken);
            await WriteAuditAsync(tenantId, currentUser, channel, release, success: false, message, auditAction, cancellationToken);
            _logger.LogWarning(
                "Channel publish skipped: connector for {ChannelType} not registered (channel={ChannelId}, release={ReleaseId})",
                channel.ChannelType,
                channel.Id,
                release.Id);
            return ToDto(release);
        }

        var context = new ChannelPublishContext(
            tenantId,
            ResolveWorkspaceLong(channel.WorkspaceId),
            channel.Id,
            channel.ChannelType,
            agentId,
            agentPublicationId,
            nextNo,
            currentUser.UserId,
            snapshotJson);

        ChannelPublishResult result;
        try
        {
            result = await connector.PublishAsync(context, cancellationToken);
        }
        catch (BusinessException ex)
        {
            release.MarkFailed(ex.Message);
            await _releaseRepository.UpdateAsync(release, cancellationToken);
            await WriteAuditAsync(tenantId, currentUser, channel, release, success: false, ex.Message, auditAction, cancellationToken);
            throw;
        }
        catch (Exception ex)
        {
            release.MarkFailed(ex.Message);
            await _releaseRepository.UpdateAsync(release, cancellationToken);
            await WriteAuditAsync(tenantId, currentUser, channel, release, success: false, ex.Message, auditAction, cancellationToken);
            _logger.LogError(ex, "Channel connector failed (channel={ChannelId}, release={ReleaseId})", channel.Id, release.Id);
            throw new BusinessException(ErrorCodes.ServerError, "ChannelConnectorFailed");
        }

        if (result.Success)
        {
            // 旧 active 转 superseded（同 channel 同时只允许一个 active）
            var prevActive = await _releaseRepository.FindActiveAsync(tenantId, channel.WorkspaceId, channel.Id, cancellationToken);
            if (prevActive is not null && prevActive.Id != release.Id)
            {
                if (rolledBackFromReleaseId is not null)
                {
                    prevActive.MarkRolledBack($"superseded-by-rollback-release-{release.Id}");
                }
                else
                {
                    prevActive.MarkSuperseded();
                }
                await _releaseRepository.UpdateAsync(prevActive, cancellationToken);
            }

            release.MarkActive(result.PublicMetadataJson ?? "{}", result.Status);
            await _releaseRepository.UpdateAsync(release, cancellationToken);
            channel.MarkAuthorized();
            await _channelRepository.UpdateAsync(channel, cancellationToken);
            await WriteAuditAsync(tenantId, currentUser, channel, release, success: true, result.Status ?? "ok", auditAction, cancellationToken);
        }
        else
        {
            var message = result.FailureReason ?? result.Status ?? "ChannelPublishFailed";
            release.MarkFailed(message);
            await _releaseRepository.UpdateAsync(release, cancellationToken);
            await WriteAuditAsync(tenantId, currentUser, channel, release, success: false, message, auditAction, cancellationToken);
        }

        return ToDto(release);
    }

    private async Task<WorkspacePublishChannel> LoadChannelOrThrowAsync(
        TenantId tenantId,
        string workspaceId,
        string channelId,
        CancellationToken cancellationToken)
    {
        if (!long.TryParse(channelId, out var id))
        {
            throw new BusinessException(ErrorCodes.NotFound, "ChannelNotFound");
        }
        var entity = await _channelRepository.FindAsync(tenantId, workspaceId, id, cancellationToken);
        if (entity is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "ChannelNotFound");
        }
        return entity;
    }

    private async Task<WorkspaceChannelRelease> LoadReleaseOrThrowAsync(
        TenantId tenantId,
        string workspaceId,
        long channelId,
        string releaseId,
        CancellationToken cancellationToken)
    {
        if (!long.TryParse(releaseId, out var id))
        {
            throw new BusinessException(ErrorCodes.NotFound, "ChannelReleaseNotFound");
        }
        var entity = await _releaseRepository.FindAsync(tenantId, workspaceId, channelId, id, cancellationToken);
        if (entity is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "ChannelReleaseNotFound");
        }
        return entity;
    }

    private async Task<AgentPublication> ResolvePublicationOrThrowAsync(
        TenantId tenantId,
        long publicationId,
        CancellationToken cancellationToken)
    {
        var entity = await _agentPublicationRepository.FindByIdAsync(tenantId, publicationId, cancellationToken);
        if (entity is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "AgentPublicationNotFound");
        }
        return entity;
    }

    private async Task WriteAuditAsync(
        TenantId tenantId,
        CurrentUserInfo currentUser,
        WorkspacePublishChannel channel,
        WorkspaceChannelRelease release,
        bool success,
        string message,
        string action,
        CancellationToken cancellationToken)
    {
        var record = new AuditRecord(
            tenantId,
            actor: currentUser.Username,
            action: action,
            result: success ? "success" : "failure",
            target: $"channel:{channel.Id}/release:{release.Id} ({channel.ChannelType}); {Truncate(message, 256)}",
            ipAddress: null,
            userAgent: null);
        try
        {
            await _auditWriter.WriteAsync(record, cancellationToken);
        }
        catch (Exception ex)
        {
            // 审计写入失败不应阻塞业务路径，但需可追踪。
            _logger.LogWarning(ex, "Failed to write audit record for {Action} (channel={ChannelId}, release={ReleaseId})",
                action, channel.Id, release.Id);
        }
    }

    internal static string BuildConfigSnapshotJson(
        WorkspacePublishChannel channel,
        long? agentId,
        long? agentPublicationId,
        string? releaseNote,
        long? sourceReleaseId)
    {
        var snapshot = new
        {
            channelId = channel.Id,
            channelType = channel.ChannelType,
            channelName = channel.Name,
            workspaceId = channel.WorkspaceId,
            supportedTargetsJson = channel.SupportedTargetsJson,
            agentId,
            agentPublicationId,
            releaseNote,
            sourceReleaseId,
            capturedAt = DateTime.UtcNow
        };
        return JsonSerializer.Serialize(snapshot, JsonOptions);
    }

    internal static WorkspaceChannelReleaseDto ToDto(WorkspaceChannelRelease entity)
    {
        return new WorkspaceChannelReleaseDto(
            Id: entity.Id.ToString(),
            WorkspaceId: entity.WorkspaceId,
            ChannelId: entity.ChannelId.ToString(),
            AgentId: entity.AgentId?.ToString(),
            AgentPublicationId: entity.AgentPublicationId?.ToString(),
            ReleaseNo: entity.ReleaseNo,
            Status: entity.Status,
            PublicMetadataJson: string.IsNullOrEmpty(entity.PublicMetadataJson) ? null : entity.PublicMetadataJson,
            ReleaseNote: string.IsNullOrEmpty(entity.ReleaseNote) ? null : entity.ReleaseNote,
            ConnectorMessage: string.IsNullOrEmpty(entity.ConnectorMessage) ? null : entity.ConnectorMessage,
            RolledBackFromReleaseId: entity.RolledBackFromReleaseId?.ToString(),
            ReleasedByUserId: entity.ReleasedByUserId,
            ReleasedAt: new DateTimeOffset(DateTime.SpecifyKind(entity.ReleasedAt, DateTimeKind.Utc)),
            CreatedAt: new DateTimeOffset(DateTime.SpecifyKind(entity.CreatedAt, DateTimeKind.Utc)),
            SupersededAt: entity.SupersededAt is null
                ? null
                : new DateTimeOffset(DateTime.SpecifyKind(entity.SupersededAt.Value, DateTimeKind.Utc)));
    }

    private static long? ParseLongOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        return long.TryParse(value, out var id) ? id : null;
    }

    private static long ResolveWorkspaceLong(string workspaceId)
    {
        return long.TryParse(workspaceId, out var id) ? id : 0L;
    }

    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }
        return value.Length <= max ? value : value[..max];
    }
}
