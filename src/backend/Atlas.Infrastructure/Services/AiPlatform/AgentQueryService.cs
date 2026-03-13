using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class AgentQueryService : IAgentQueryService
{
    private readonly AgentRepository _agentRepository;
    private readonly AgentKnowledgeLinkRepository _linkRepository;

    public AgentQueryService(AgentRepository agentRepository, AgentKnowledgeLinkRepository linkRepository)
    {
        _agentRepository = agentRepository;
        _linkRepository = linkRepository;
    }

    public async Task<PagedResult<AgentListItem>> GetPagedAsync(
        TenantId tenantId,
        string? keyword,
        string? status,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var statusFilter = ParseStatus(status);
        var (items, total) = await _agentRepository.GetPagedAsync(
            tenantId,
            keyword,
            statusFilter,
            pageIndex,
            pageSize,
            cancellationToken);
        var result = items.Select(MapListItem).ToList();
        return new PagedResult<AgentListItem>(result, total, pageIndex, pageSize);
    }

    public async Task<AgentDetail?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var entity = await _agentRepository.FindByIdAsync(tenantId, id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var links = await _linkRepository.GetByAgentIdAsync(tenantId, id, cancellationToken);
        return new AgentDetail(
            entity.Id,
            entity.Name,
            entity.Description,
            entity.AvatarUrl,
            entity.SystemPrompt,
            entity.ModelConfigId,
            entity.ModelName,
            entity.Temperature,
            entity.MaxTokens,
            entity.Status.ToString(),
            entity.CreatorId,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.PublishedAt,
            entity.PublishVersion,
            links.Select(x => x.KnowledgeBaseId).ToArray());
    }

    private static AgentListItem MapListItem(Agent entity)
        => new(
            entity.Id,
            entity.Name,
            entity.Description,
            entity.AvatarUrl,
            entity.Status.ToString(),
            entity.ModelName,
            entity.CreatedAt,
            entity.PublishVersion);

    private static AgentStatus? ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        if (Enum.TryParse<AgentStatus>(status, true, out var parsed))
        {
            return parsed;
        }

        return null;
    }
}
