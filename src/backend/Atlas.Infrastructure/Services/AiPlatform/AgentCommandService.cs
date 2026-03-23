using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class AgentCommandService : IAgentCommandService
{
    private readonly AgentRepository _agentRepository;
    private readonly AgentKnowledgeLinkRepository _linkRepository;
    private readonly AgentPluginBindingRepository _pluginBindingRepository;
    private readonly AiPluginRepository _pluginRepository;
    private readonly ModelConfigRepository _modelConfigRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IUnitOfWork _unitOfWork;

    public AgentCommandService(
        AgentRepository agentRepository,
        AgentKnowledgeLinkRepository linkRepository,
        AgentPluginBindingRepository pluginBindingRepository,
        AiPluginRepository pluginRepository,
        ModelConfigRepository modelConfigRepository,
        IIdGeneratorAccessor idGeneratorAccessor,
        IUnitOfWork unitOfWork)
    {
        _agentRepository = agentRepository;
        _linkRepository = linkRepository;
        _pluginBindingRepository = pluginBindingRepository;
        _pluginRepository = pluginRepository;
        _modelConfigRepository = modelConfigRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
        _unitOfWork = unitOfWork;
    }

    public async Task<long> CreateAsync(
        TenantId tenantId,
        long creatorId,
        AgentCreateRequest request,
        CancellationToken cancellationToken)
    {
        var exists = await _agentRepository.ExistsByNameAsync(tenantId, request.Name, cancellationToken);
        if (exists)
        {
            throw new BusinessException("AgentNameExists", ErrorCodes.ValidationError);
        }

        var modelConfigId = await ResolveModelConfigIdAsync(tenantId, request.ModelConfigId, cancellationToken);
        var entity = new Agent(tenantId, request.Name, creatorId, _idGeneratorAccessor.NextId());
        entity.Update(
            request.Name,
            request.Description,
            avatarUrl: null,
            request.SystemPrompt,
            modelConfigId,
            request.ModelName,
            request.Temperature,
            request.MaxTokens);

        await _agentRepository.AddAsync(entity, cancellationToken);
        return entity.Id;
    }

    public async Task UpdateAsync(TenantId tenantId, long id, AgentUpdateRequest request, CancellationToken cancellationToken)
    {
        var entity = await _agentRepository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("AgentNotFound", ErrorCodes.NotFound);

        var modelConfigId = await ResolveModelConfigIdAsync(tenantId, request.ModelConfigId, cancellationToken);
        entity.Update(
            request.Name,
            request.Description,
            request.AvatarUrl,
            request.SystemPrompt,
            modelConfigId,
            request.ModelName,
            request.Temperature,
            request.MaxTokens);

        var knowledgeIds = request.KnowledgeBaseIds?
            .Where(x => x > 0)
            .Distinct()
            .ToArray() ?? [];

        var pluginBindings = await BuildValidatedPluginBindingsAsync(
            tenantId,
            id,
            request.PluginBindings,
            cancellationToken);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _agentRepository.UpdateAsync(entity, cancellationToken);
            await _linkRepository.DeleteByAgentIdAsync(tenantId, id, cancellationToken);
            if (knowledgeIds.Length == 0)
            {
                await _pluginBindingRepository.DeleteByAgentIdAsync(tenantId, id, cancellationToken);
                if (pluginBindings.Length > 0)
                {
                    await _pluginBindingRepository.AddRangeAsync(pluginBindings, cancellationToken);
                }

                return;
            }

            var knowledgeLinks = knowledgeIds
                .Select(knowledgeBaseId =>
                    new AgentKnowledgeLink(tenantId, id, knowledgeBaseId, _idGeneratorAccessor.NextId()))
                .ToArray();
            await _linkRepository.AddRangeAsync(knowledgeLinks, cancellationToken);

            await _pluginBindingRepository.DeleteByAgentIdAsync(tenantId, id, cancellationToken);
            if (pluginBindings.Length > 0)
            {
                await _pluginBindingRepository.AddRangeAsync(pluginBindings, cancellationToken);
            }
        }, cancellationToken);
    }

    public async Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var entity = await _agentRepository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("AgentNotFound", ErrorCodes.NotFound);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _linkRepository.DeleteByAgentIdAsync(tenantId, entity.Id, cancellationToken);
            await _agentRepository.DeleteAsync(tenantId, entity.Id, cancellationToken);
        }, cancellationToken);
    }

    public async Task<long> DuplicateAsync(TenantId tenantId, long creatorId, long id, CancellationToken cancellationToken)
    {
        var source = await _agentRepository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("AgentNotFound", ErrorCodes.NotFound);

        var links = await _linkRepository.GetByAgentIdAsync(tenantId, id, cancellationToken);
        var pluginBindings = await _pluginBindingRepository.GetByAgentIdAsync(tenantId, id, cancellationToken);
        var duplicateId = _idGeneratorAccessor.NextId();
        var duplicate = source.CreateDuplicate(duplicateId, $"Copy of {source.Name}", creatorId);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _agentRepository.AddAsync(duplicate, cancellationToken);
            if (links.Count > 0)
            {
                var clonedLinks = links
                    .Select(link => new AgentKnowledgeLink(
                        tenantId,
                        duplicateId,
                        link.KnowledgeBaseId,
                        _idGeneratorAccessor.NextId()))
                    .ToArray();
                await _linkRepository.AddRangeAsync(clonedLinks, cancellationToken);
            }

            if (pluginBindings.Count > 0)
            {
                var clonedBindings = pluginBindings
                    .Select(binding => new AgentPluginBinding(
                        tenantId,
                        duplicateId,
                        binding.PluginId,
                        binding.SortOrder,
                        binding.IsEnabled,
                        binding.ToolConfigJson,
                        _idGeneratorAccessor.NextId()))
                    .ToArray();
                await _pluginBindingRepository.AddRangeAsync(clonedBindings, cancellationToken);
            }
        }, cancellationToken);

        return duplicateId;
    }

    public async Task PublishAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var entity = await _agentRepository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("AgentNotFound", ErrorCodes.NotFound);

        entity.Publish();
        await _agentRepository.UpdateAsync(entity, cancellationToken);
    }

    private async Task<long?> ResolveModelConfigIdAsync(
        TenantId tenantId,
        long? requestedModelConfigId,
        CancellationToken cancellationToken)
    {
        if (requestedModelConfigId.HasValue)
        {
            var exists = await _modelConfigRepository.FindByIdAsync(tenantId, requestedModelConfigId.Value, cancellationToken);
            if (exists is null)
            {
                throw new BusinessException("ModelConfigNotFound", ErrorCodes.ValidationError);
            }

            return requestedModelConfigId.Value;
        }

        var enabled = await _modelConfigRepository.GetAllEnabledAsync(tenantId, cancellationToken);
        return enabled.FirstOrDefault()?.Id;
    }

    private async Task<AgentPluginBinding[]> BuildValidatedPluginBindingsAsync(
        TenantId tenantId,
        long agentId,
        IReadOnlyList<AgentPluginBindingInput>? inputs,
        CancellationToken cancellationToken)
    {
        var normalizedInputs = inputs?
            .Where(x => x.PluginId > 0)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.PluginId)
            .GroupBy(x => x.PluginId)
            .Select(g => g.First())
            .ToArray() ?? [];

        if (normalizedInputs.Length == 0)
        {
            return [];
        }

        var pluginIds = normalizedInputs
            .Select(x => x.PluginId)
            .Distinct()
            .ToArray();
        var existingPlugins = await _pluginRepository.QueryByIdsAsync(tenantId, pluginIds, cancellationToken);
        if (existingPlugins.Count != pluginIds.Length)
        {
            throw new BusinessException("存在无效的插件绑定。", ErrorCodes.ValidationError);
        }

        return normalizedInputs
            .Select(binding => new AgentPluginBinding(
                tenantId,
                agentId,
                binding.PluginId,
                binding.SortOrder,
                binding.IsEnabled,
                binding.ToolConfigJson,
                _idGeneratorAccessor.NextId()))
            .ToArray();
    }
}
