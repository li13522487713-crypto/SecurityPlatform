using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class AiMemoryService : IAiMemoryService
{
    private readonly LongTermMemoryRepository _longTermMemoryRepository;

    public AiMemoryService(LongTermMemoryRepository longTermMemoryRepository)
    {
        _longTermMemoryRepository = longTermMemoryRepository;
    }

    public async Task<PagedResult<LongTermMemoryListItem>> GetLongTermMemoriesAsync(
        TenantId tenantId,
        long userId,
        long? agentId,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var (items, total) = await _longTermMemoryRepository.GetPagedByUserAsync(
            tenantId,
            userId,
            agentId,
            keyword,
            pageIndex,
            pageSize,
            cancellationToken);
        var list = items.Select(item => new LongTermMemoryListItem(
            item.Id,
            item.AgentId,
            item.ConversationId,
            item.MemoryKey,
            item.Content,
            item.Source,
            item.HitCount,
            item.LastReferencedAt,
            item.CreatedAt,
            item.UpdatedAt)).ToList();
        return new PagedResult<LongTermMemoryListItem>(list, total, pageIndex, pageSize);
    }

    public async Task DeleteLongTermMemoryAsync(
        TenantId tenantId,
        long userId,
        long memoryId,
        CancellationToken cancellationToken)
    {
        var affectedRows = await _longTermMemoryRepository.DeleteByUserAsync(
            tenantId,
            userId,
            memoryId,
            cancellationToken);
        if (affectedRows <= 0)
        {
            throw new BusinessException("LongTermMemoryNotFound", ErrorCodes.NotFound);
        }
    }

    public Task<int> ClearLongTermMemoriesAsync(
        TenantId tenantId,
        long userId,
        long? agentId,
        CancellationToken cancellationToken)
    {
        return _longTermMemoryRepository.DeleteByUserAndAgentAsync(
            tenantId,
            userId,
            agentId,
            cancellationToken);
    }
}
