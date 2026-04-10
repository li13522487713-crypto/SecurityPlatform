using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Core.Abstractions;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>
/// 长期记忆节点：支持读/写/删。
/// </summary>
public sealed class LtmNodeExecutor : INodeExecutor
{
    private readonly LongTermMemoryRepository _longTermMemoryRepository;
    private readonly IAiMemoryService _aiMemoryService;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public LtmNodeExecutor(
        LongTermMemoryRepository longTermMemoryRepository,
        IAiMemoryService aiMemoryService,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _longTermMemoryRepository = longTermMemoryRepository;
        _aiMemoryService = aiMemoryService;
        _idGeneratorAccessor = idGeneratorAccessor;
    }

    public WorkflowNodeType NodeType => WorkflowNodeType.Ltm;

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var action = context.GetConfigString("action", "read").Trim().ToLowerInvariant();
        var userId = ResolveLong(context, "userId", "user_id");
        var agentId = ResolveLong(context, "agentId", "agent_id");
        var conversationId = ResolveLong(context, "conversationId", "conversation_id");
        var memoryKey = context.GetConfigString("memoryKey");

        if (userId <= 0)
        {
            return new NodeExecutionResult(false, outputs, "LTM 节点缺少 userId。");
        }

        switch (action)
        {
            case "write":
            {
                if (agentId <= 0)
                {
                    return new NodeExecutionResult(false, outputs, "LTM 写入缺少 agentId。");
                }

                if (string.IsNullOrWhiteSpace(memoryKey))
                {
                    return new NodeExecutionResult(false, outputs, "LTM 写入缺少 memoryKey。");
                }

                var content = context.ReplaceVariables(context.GetConfigString("content"));
                var source = context.GetConfigString("source", "workflow");
                var existing = await _longTermMemoryRepository.QueryByKeysAsync(
                    context.TenantId,
                    userId,
                    agentId,
                    [memoryKey],
                    cancellationToken);
                var target = existing.FirstOrDefault();
                if (target is null)
                {
                    target = new LongTermMemory(
                        context.TenantId,
                        userId,
                        agentId,
                        Math.Max(0, conversationId),
                        memoryKey,
                        content,
                        source,
                        _idGeneratorAccessor.NextId());
                    await _longTermMemoryRepository.AddAsync(target, cancellationToken);
                }
                else
                {
                    target.Reinforce(content, source, Math.Max(0, conversationId));
                    await _longTermMemoryRepository.UpdateAsync(target, cancellationToken);
                }

                outputs["ltm_action"] = VariableResolver.CreateStringElement("write");
                outputs["ltm_memory_id"] = JsonSerializer.SerializeToElement(target.Id);
                outputs["ltm_memory_key"] = VariableResolver.CreateStringElement(memoryKey);
                return new NodeExecutionResult(true, outputs);
            }
            case "delete":
            {
                var memoryId = ResolveLong(context, "memoryId", "memory_id");
                if (memoryId > 0)
                {
                    await _aiMemoryService.DeleteLongTermMemoryAsync(context.TenantId, userId, memoryId, cancellationToken);
                    outputs["affected_rows"] = JsonSerializer.SerializeToElement(1);
                }
                else
                {
                    var affected = await _aiMemoryService.ClearLongTermMemoriesAsync(
                        context.TenantId,
                        userId,
                        agentId > 0 ? agentId : null,
                        cancellationToken);
                    outputs["affected_rows"] = JsonSerializer.SerializeToElement(affected);
                }

                outputs["ltm_action"] = VariableResolver.CreateStringElement("delete");
                return new NodeExecutionResult(true, outputs);
            }
            default:
            {
                if (agentId <= 0)
                {
                    return new NodeExecutionResult(false, outputs, "LTM 读取缺少 agentId。");
                }

                var limit = Math.Clamp(context.GetConfigInt32("limit", 20), 1, 100);
                var memories = await _longTermMemoryRepository.ListByUserAgentAsync(
                    context.TenantId,
                    userId,
                    agentId,
                    limit,
                    cancellationToken);
                if (!string.IsNullOrWhiteSpace(memoryKey))
                {
                    memories = memories
                        .Where(x => string.Equals(x.MemoryKey, memoryKey, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                outputs["ltm_action"] = VariableResolver.CreateStringElement("read");
                outputs["memories"] = JsonSerializer.SerializeToElement(memories.Select(x => new
                {
                    x.Id,
                    x.MemoryKey,
                    x.Content,
                    x.Source,
                    x.HitCount,
                    x.LastReferencedAt
                }));
                return new NodeExecutionResult(true, outputs);
            }
        }
    }

    private static long ResolveLong(NodeExecutionContext context, string configKey, string variablePath)
    {
        var value = context.GetConfigInt64(configKey, 0L);
        if (value > 0)
        {
            return value;
        }

        return context.TryResolveVariable(variablePath, out var resolved) &&
               long.TryParse(VariableResolver.ToDisplayText(resolved), out var parsed)
            ? parsed
            : 0L;
    }
}
