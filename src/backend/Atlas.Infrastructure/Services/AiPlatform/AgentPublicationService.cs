using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text.Json;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class AgentPublicationService : IAgentPublicationService
{
    private readonly AgentRepository _agentRepository;
    private readonly AgentKnowledgeLinkRepository _knowledgeLinkRepository;
    private readonly AgentPluginBindingRepository _pluginBindingRepository;
    private readonly AgentPublicationRepository _publicationRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AgentPublicationOption _options;

    public AgentPublicationService(
        AgentRepository agentRepository,
        AgentKnowledgeLinkRepository knowledgeLinkRepository,
        AgentPluginBindingRepository pluginBindingRepository,
        AgentPublicationRepository publicationRepository,
        IIdGeneratorAccessor idGeneratorAccessor,
        IUnitOfWork unitOfWork,
        IOptions<AiPlatformOptions> options)
    {
        _agentRepository = agentRepository;
        _knowledgeLinkRepository = knowledgeLinkRepository;
        _pluginBindingRepository = pluginBindingRepository;
        _publicationRepository = publicationRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
        _unitOfWork = unitOfWork;
        _options = options.Value.Publication;
    }

    public async Task<IReadOnlyList<AgentPublicationListItem>> GetByAgentAsync(
        TenantId tenantId,
        long agentId,
        CancellationToken cancellationToken)
    {
        var records = await _publicationRepository.GetByAgentIdAsync(tenantId, agentId, cancellationToken);
        return records.Select(MapRecord).ToArray();
    }

    public async Task<AgentPublicationPublishResult> PublishAsync(
        TenantId tenantId,
        long agentId,
        long publisherUserId,
        AgentPublicationPublishRequest request,
        CancellationToken cancellationToken)
    {
        var agent = await _agentRepository.FindByIdAsync(tenantId, agentId, cancellationToken)
            ?? throw new BusinessException("AgentNotFound", ErrorCodes.NotFound);
        var knowledgeLinks = await _knowledgeLinkRepository.GetByAgentIdAsync(tenantId, agentId, cancellationToken);
        var pluginBindings = await _pluginBindingRepository.GetByAgentIdAsync(tenantId, agentId, cancellationToken);
        var nextVersion = await _publicationRepository.GetLatestVersionAsync(tenantId, agentId, cancellationToken) + 1;
        var embedToken = GenerateEmbedToken();
        var tokenExpiresAt = DateTime.UtcNow.AddHours(Math.Max(1, _options.EmbedTokenTtlHours));
        var snapshotJson = BuildSnapshotJson(agent, knowledgeLinks, pluginBindings);
        var publication = new AgentPublication(
            tenantId,
            agentId,
            nextVersion,
            snapshotJson,
            embedToken,
            tokenExpiresAt,
            request.ReleaseNote?.Trim(),
            publisherUserId,
            _idGeneratorAccessor.NextId());

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _publicationRepository.DeactivateActiveByAgentIdAsync(tenantId, agentId, cancellationToken);
            agent.Publish();
            await _agentRepository.UpdateAsync(agent, cancellationToken);
            await _publicationRepository.AddAsync(publication, cancellationToken);
        }, cancellationToken);

        return new AgentPublicationPublishResult(
            publication.Id,
            publication.AgentId,
            publication.Version,
            publication.EmbedToken,
            publication.EmbedTokenExpiresAt);
    }

    public async Task<AgentPublicationPublishResult> RollbackAsync(
        TenantId tenantId,
        long agentId,
        long publisherUserId,
        AgentPublicationRollbackRequest request,
        CancellationToken cancellationToken)
    {
        var agent = await _agentRepository.FindByIdAsync(tenantId, agentId, cancellationToken)
            ?? throw new BusinessException("AgentNotFound", ErrorCodes.NotFound);
        var target = await _publicationRepository.FindByAgentAndVersionAsync(
            tenantId,
            agentId,
            request.TargetVersion,
            cancellationToken)
            ?? throw new BusinessException("TargetPublicationVersionNotFound", ErrorCodes.NotFound);
        var nextVersion = await _publicationRepository.GetLatestVersionAsync(tenantId, agentId, cancellationToken) + 1;
        var embedToken = GenerateEmbedToken();
        var tokenExpiresAt = DateTime.UtcNow.AddHours(Math.Max(1, _options.EmbedTokenTtlHours));
        var note = string.IsNullOrWhiteSpace(request.ReleaseNote)
            ? $"Rollback from v{request.TargetVersion}"
            : request.ReleaseNote.Trim();
        var rolledPublication = new AgentPublication(
            tenantId,
            agentId,
            nextVersion,
            target.SnapshotJson,
            embedToken,
            tokenExpiresAt,
            note,
            publisherUserId,
            _idGeneratorAccessor.NextId());

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _publicationRepository.DeactivateActiveByAgentIdAsync(tenantId, agentId, cancellationToken);
            agent.Publish();
            await _agentRepository.UpdateAsync(agent, cancellationToken);
            await _publicationRepository.AddAsync(rolledPublication, cancellationToken);
        }, cancellationToken);

        return new AgentPublicationPublishResult(
            rolledPublication.Id,
            rolledPublication.AgentId,
            rolledPublication.Version,
            rolledPublication.EmbedToken,
            rolledPublication.EmbedTokenExpiresAt);
    }

    public async Task<AgentEmbedTokenResult> RegenerateEmbedTokenAsync(
        TenantId tenantId,
        long agentId,
        CancellationToken cancellationToken)
    {
        var active = await _publicationRepository.FindActiveByAgentIdAsync(tenantId, agentId, cancellationToken)
            ?? throw new BusinessException("ActiveAgentPublicationNotFound", ErrorCodes.NotFound);
        var embedToken = GenerateEmbedToken();
        var tokenExpiresAt = DateTime.UtcNow.AddHours(Math.Max(1, _options.EmbedTokenTtlHours));
        active.RotateEmbedToken(embedToken, tokenExpiresAt);
        await _publicationRepository.UpdateAsync(active, cancellationToken);
        return new AgentEmbedTokenResult(
            active.Id,
            active.AgentId,
            active.Version,
            active.EmbedToken,
            active.EmbedTokenExpiresAt);
    }

    public async Task<AgentPublicationTokenContext> ResolveByEmbedTokenAsync(
        string embedToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(embedToken))
        {
            throw new BusinessException("EmbedTokenRequired", ErrorCodes.ValidationError);
        }

        var publication = await _publicationRepository.FindByEmbedTokenAsync(embedToken.Trim(), cancellationToken)
            ?? throw new BusinessException("EmbedTokenInvalid", ErrorCodes.Unauthorized);
        if (!publication.IsActive)
        {
            throw new BusinessException("EmbedTokenInactive", ErrorCodes.Forbidden);
        }

        if (publication.EmbedTokenExpiresAt <= DateTime.UtcNow)
        {
            throw new BusinessException("EmbedTokenExpired", ErrorCodes.Unauthorized);
        }

        return new AgentPublicationTokenContext(
            new TenantId(publication.TenantIdValue),
            publication.AgentId,
            publication.Id,
            publication.Version);
    }

    private static string BuildSnapshotJson(
        Agent agent,
        IReadOnlyList<AgentKnowledgeLink> knowledgeLinks,
        IReadOnlyList<AgentPluginBinding> pluginBindings)
    {
        var snapshot = new
        {
            agent = new
            {
                agent.Id,
                agent.Name,
                agent.Description,
                agent.AvatarUrl,
                agent.SystemPrompt,
                agent.ModelConfigId,
                agent.ModelName,
                agent.Temperature,
                agent.MaxTokens,
                agent.EnableMemory,
                agent.EnableShortTermMemory,
                agent.EnableLongTermMemory,
                agent.LongTermMemoryTopK
            },
            knowledgeBaseIds = knowledgeLinks
                .Select(x => x.KnowledgeBaseId)
                .Distinct()
                .OrderBy(x => x)
                .ToArray(),
            pluginBindings = pluginBindings
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.PluginId)
                .Select(x => new
                {
                    x.PluginId,
                    x.SortOrder,
                    x.IsEnabled,
                    x.ToolConfigJson
                })
                .ToArray(),
            generatedAt = DateTime.UtcNow
        };

        return JsonSerializer.Serialize(snapshot);
    }

    private static AgentPublicationListItem MapRecord(AgentPublication record)
        => new(
            record.Id,
            record.AgentId,
            record.Version,
            record.IsActive,
            record.EmbedToken,
            record.EmbedTokenExpiresAt,
            record.ReleaseNote,
            record.PublishedByUserId,
            record.CreatedAt,
            record.UpdatedAt,
            record.RevokedAt.HasValue && record.RevokedAt.Value > DateTime.UnixEpoch
                ? record.RevokedAt
                : null);

    private static string GenerateEmbedToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
