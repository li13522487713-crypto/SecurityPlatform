using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class SqlMemoryProvider : IMemoryProvider
{
    private readonly LongTermMemoryRepository _repository;
    private readonly IIdGeneratorAccessor _idGenerator;

    public SqlMemoryProvider(
        LongTermMemoryRepository repository,
        IIdGeneratorAccessor idGenerator)
    {
        _repository = repository;
        _idGenerator = idGenerator;
    }

    public async Task UpsertAsync(
        TenantId tenantId,
        MemoryNamespace memoryNamespace,
        long userId,
        long agentId,
        IReadOnlyCollection<MemoryRecordInput> records,
        CancellationToken cancellationToken = default)
    {
        if (records.Count == 0)
        {
            return;
        }

        var normalizedInputs = records
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .Select(item => new
            {
                NamespacedKey = BuildNamespacedKey(memoryNamespace, item.Key),
                item.Content,
                item.Source,
                item.ConversationId
            })
            .GroupBy(item => item.NamespacedKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToArray();
        if (normalizedInputs.Length == 0)
        {
            return;
        }

        var keyArray = normalizedInputs.Select(item => item.NamespacedKey).ToArray();
        var existing = await _repository.QueryByKeysAsync(
            tenantId,
            userId,
            agentId,
            keyArray,
            cancellationToken);
        var existingMap = existing.ToDictionary(item => item.MemoryKey, StringComparer.OrdinalIgnoreCase);

        var toAdd = new List<LongTermMemory>();
        var toUpdate = new List<LongTermMemory>();
        foreach (var item in normalizedInputs)
        {
            if (existingMap.TryGetValue(item.NamespacedKey, out var memory))
            {
                memory.Reinforce(
                    item.Content,
                    item.Source,
                    item.ConversationId.GetValueOrDefault(memory.ConversationId));
                toUpdate.Add(memory);
            }
            else
            {
                toAdd.Add(new LongTermMemory(
                    tenantId,
                    userId,
                    agentId,
                    item.ConversationId.GetValueOrDefault(0),
                    item.NamespacedKey,
                    item.Content,
                    item.Source,
                    _idGenerator.NextId()));
            }
        }

        if (toAdd.Count > 0)
        {
            await _repository.AddRangeAsync(toAdd, cancellationToken);
        }

        if (toUpdate.Count > 0)
        {
            await _repository.UpdateRangeAsync(toUpdate, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<MemoryRecord>> GetRecentAsync(
        TenantId tenantId,
        MemoryNamespace memoryNamespace,
        long userId,
        long agentId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var rows = await _repository.ListByUserAgentAsync(
            tenantId,
            userId,
            agentId,
            limit,
            cancellationToken);
        return rows
            .Where(item => IsInNamespace(memoryNamespace, item.MemoryKey))
            .Select(ToRecord)
            .ToArray();
    }

    public async Task<IReadOnlyList<MemoryRecord>> GetByKeysAsync(
        TenantId tenantId,
        MemoryNamespace memoryNamespace,
        long userId,
        long agentId,
        IReadOnlyCollection<string> keys,
        CancellationToken cancellationToken = default)
    {
        if (keys.Count == 0)
        {
            return Array.Empty<MemoryRecord>();
        }

        var normalizedKeys = keys
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => BuildNamespacedKey(memoryNamespace, item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalizedKeys.Length == 0)
        {
            return Array.Empty<MemoryRecord>();
        }

        var rows = await _repository.QueryByKeysAsync(
            tenantId,
            userId,
            agentId,
            normalizedKeys,
            cancellationToken);
        return rows.Select(ToRecord).ToArray();
    }

    private static MemoryRecord ToRecord(LongTermMemory entity)
        => new(
            entity.Id,
            entity.MemoryKey,
            entity.Content,
            entity.Source,
            entity.HitCount,
            entity.LastReferencedAt,
            entity.CreatedAt,
            entity.UpdatedAt);

    private static string BuildNamespacedKey(MemoryNamespace memoryNamespace, string key)
        => $"{memoryNamespace.NormalizedScope}:{key.Trim()}";

    private static bool IsInNamespace(MemoryNamespace memoryNamespace, string memoryKey)
        => memoryKey.StartsWith($"{memoryNamespace.NormalizedScope}:", StringComparison.OrdinalIgnoreCase);
}
