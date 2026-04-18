using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Core.Abstractions;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>
/// 单一变量读取节点（M20 P0-3，与 VariableAggregator(32) 区分）。
/// Config：
///  - variableKey（必填，目标变量键，支持 dot path：a.b.c[0]）
///  - outputKey（默认 "variable_value"）
///  - defaultValue（变量不存在时的兜底字面量；为空字符串则输出 null）
/// </summary>
public sealed class VariableNodeExecutor : INodeExecutor
{
    public WorkflowNodeType NodeType => WorkflowNodeType.Variable;

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var variableKey = context.GetConfigString("variableKey");
        if (string.IsNullOrWhiteSpace(variableKey))
        {
            return Task.FromResult(new NodeExecutionResult(false, outputs, "Variable 节点缺少 variableKey。"));
        }

        var outputKey = context.GetConfigString("outputKey", "variable_value");
        if (context.TryResolveVariable(variableKey, out var resolved))
        {
            outputs[outputKey] = resolved;
        }
        else
        {
            var def = context.GetConfigString("defaultValue");
            outputs[outputKey] = string.IsNullOrEmpty(def)
                ? JsonSerializer.SerializeToElement<object?>(null)
                : context.ParseLiteralOrTemplate(def);
        }

        outputs["resolved"] = JsonSerializer.SerializeToElement(context.TryResolveVariable(variableKey, out _));
        return Task.FromResult(new NodeExecutionResult(true, outputs));
    }
}

/// <summary>
/// 场景变量节点（M20 上游对齐 SceneVariable=24）。
/// 提供 scene 范围内的临时变量读写能力，作用域由 sceneId 隔离。
/// Config：
///  - action（read/write，默认 read）
///  - sceneId（必填，作为变量命名空间前缀）
///  - key（必填）
///  - value（write 时必填，支持模板）
/// </summary>
public sealed class SceneVariableNodeExecutor : INodeExecutor
{
    public WorkflowNodeType NodeType => WorkflowNodeType.SceneVariable;

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var sceneId = context.GetConfigString("sceneId");
        var key = context.GetConfigString("key");
        if (string.IsNullOrWhiteSpace(sceneId) || string.IsNullOrWhiteSpace(key))
        {
            return Task.FromResult(new NodeExecutionResult(false, outputs, "SceneVariable 节点缺少 sceneId 或 key。"));
        }

        var action = context.GetConfigString("action", "read").Trim().ToLowerInvariant();
        var fullKey = $"scene.{sceneId}.{key}";

        if (action == "write")
        {
            var raw = context.GetConfigString("value");
            outputs[fullKey] = context.ParseLiteralOrTemplate(raw);
            outputs["scene_action"] = VariableResolver.CreateStringElement("write");
            outputs["scene_full_key"] = VariableResolver.CreateStringElement(fullKey);
            return Task.FromResult(new NodeExecutionResult(true, outputs));
        }

        // read
        if (context.TryResolveVariable(fullKey, out var v))
        {
            outputs["scene_value"] = v;
        }
        else
        {
            outputs["scene_value"] = JsonSerializer.SerializeToElement<object?>(null);
        }
        outputs["scene_action"] = VariableResolver.CreateStringElement("read");
        outputs["scene_full_key"] = VariableResolver.CreateStringElement(fullKey);
        return Task.FromResult(new NodeExecutionResult(true, outputs));
    }
}

/// <summary>
/// 场景对话节点（M20 上游对齐 SceneChat=25）。
/// 简化语义：基于已有 Llm 节点协议，把"场景对话"抽象为一次"指令型 outputs"返回，
/// 让前端 dispatch / chatflow 在收到 outputs 后接入真实模型流；与 ImageGeneration 同模式。
/// Config：
///  - sceneId（必填）
///  - prompt（必填，对话提示词模板）
///  - inputKey（默认 "input"，从变量取用户输入）
/// 输出：scene_chat_request（JsonObject）。
/// </summary>
public sealed class SceneChatNodeExecutor : INodeExecutor
{
    public WorkflowNodeType NodeType => WorkflowNodeType.SceneChat;

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var sceneId = context.GetConfigString("sceneId");
        var prompt = context.GetConfigString("prompt");
        if (string.IsNullOrWhiteSpace(sceneId) || string.IsNullOrWhiteSpace(prompt))
        {
            return Task.FromResult(new NodeExecutionResult(false, outputs, "SceneChat 节点缺少 sceneId 或 prompt。"));
        }

        var inputKey = context.GetConfigString("inputKey", "input");
        context.TryResolveVariable(inputKey, out var inputElem);
        var renderedPrompt = context.ReplaceVariables(prompt);

        outputs["scene_chat_request"] = JsonSerializer.SerializeToElement(new
        {
            sceneId,
            prompt = renderedPrompt,
            input = inputElem.ValueKind == JsonValueKind.Undefined ? null : (object?)inputElem
        });
        outputs["scene_id"] = VariableResolver.CreateStringElement(sceneId);
        return Task.FromResult(new NodeExecutionResult(true, outputs));
    }
}

/// <summary>
/// 上游长期记忆节点（M20 上游对齐 LtmUpstream=26）。
/// 与 Atlas Ltm(62) 联动：内部桥接到现有 LtmNodeExecutor 的 read 路径，
/// 但 NodeType 暴露为 LtmUpstream，便于上游 schema 兼容。
/// Config：与 Ltm 相同（action=read，默认；userId/agentId/memoryKey/limit）。
/// </summary>
public sealed class LtmUpstreamNodeExecutor : INodeExecutor
{
    private readonly LongTermMemoryRepository _longTermMemoryRepository;

    public LtmUpstreamNodeExecutor(LongTermMemoryRepository longTermMemoryRepository)
    {
        _longTermMemoryRepository = longTermMemoryRepository;
    }

    public WorkflowNodeType NodeType => WorkflowNodeType.LtmUpstream;

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var userId = context.GetConfigInt64("userId");
        var agentId = context.GetConfigInt64("agentId");
        var memoryKey = context.GetConfigString("memoryKey");
        var limit = Math.Clamp(context.GetConfigInt32("limit", 20), 1, 100);

        if (userId <= 0 || agentId <= 0)
        {
            return new NodeExecutionResult(false, outputs, "LtmUpstream 缺少 userId 或 agentId。");
        }

        var memories = await _longTermMemoryRepository.ListByUserAgentAsync(
            context.TenantId, userId, agentId, limit, cancellationToken);
        if (!string.IsNullOrWhiteSpace(memoryKey))
        {
            memories = memories
                .Where(x => string.Equals(x.MemoryKey, memoryKey, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        outputs["ltm_action"] = VariableResolver.CreateStringElement("read");
        outputs["memories"] = JsonSerializer.SerializeToElement(memories.Select(x => new
        {
            x.Id, x.MemoryKey, x.Content, x.Source, x.HitCount, x.LastReferencedAt
        }));
        outputs["upstream"] = JsonSerializer.SerializeToElement(true);
        return new NodeExecutionResult(true, outputs);
    }
}

/// <summary>
/// 记忆读取节点（M20 P0-3 拆分自 Ltm，MemoryRead=64）。
/// Config：userId / agentId（必填）/ memoryKey（可选）/ limit（默认 20）。
/// </summary>
public sealed class MemoryReadNodeExecutor : INodeExecutor
{
    private readonly LongTermMemoryRepository _repo;

    public MemoryReadNodeExecutor(LongTermMemoryRepository repo)
    {
        _repo = repo;
    }

    public WorkflowNodeType NodeType => WorkflowNodeType.MemoryRead;

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var userId = context.GetConfigInt64("userId");
        var agentId = context.GetConfigInt64("agentId");
        if (userId <= 0 || agentId <= 0)
        {
            return new NodeExecutionResult(false, outputs, "MemoryRead 缺少 userId 或 agentId。");
        }

        var limit = Math.Clamp(context.GetConfigInt32("limit", 20), 1, 100);
        var memoryKey = context.GetConfigString("memoryKey");
        var list = await _repo.ListByUserAgentAsync(context.TenantId, userId, agentId, limit, cancellationToken);
        if (!string.IsNullOrWhiteSpace(memoryKey))
        {
            list = list.Where(x => string.Equals(x.MemoryKey, memoryKey, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        outputs["memories"] = JsonSerializer.SerializeToElement(list.Select(x => new
        {
            x.Id, x.MemoryKey, x.Content, x.Source, x.HitCount
        }));
        outputs["count"] = JsonSerializer.SerializeToElement(list.Count);
        return new NodeExecutionResult(true, outputs);
    }
}

/// <summary>
/// 记忆写入节点（M20 P0-3 拆分自 Ltm，MemoryWrite=65）。
/// Config：userId / agentId / memoryKey / content（必填）/ source（默认 "workflow"）/ conversationId（可选）。
/// </summary>
public sealed class MemoryWriteNodeExecutor : INodeExecutor
{
    private readonly LongTermMemoryRepository _repo;
    private readonly IIdGeneratorAccessor _idGen;

    public MemoryWriteNodeExecutor(LongTermMemoryRepository repo, IIdGeneratorAccessor idGen)
    {
        _repo = repo;
        _idGen = idGen;
    }

    public WorkflowNodeType NodeType => WorkflowNodeType.MemoryWrite;

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var userId = context.GetConfigInt64("userId");
        var agentId = context.GetConfigInt64("agentId");
        var memoryKey = context.GetConfigString("memoryKey");
        var content = context.ReplaceVariables(context.GetConfigString("content"));
        if (userId <= 0 || agentId <= 0 || string.IsNullOrWhiteSpace(memoryKey) || string.IsNullOrWhiteSpace(content))
        {
            return new NodeExecutionResult(false, outputs, "MemoryWrite 缺少 userId / agentId / memoryKey / content。");
        }

        var source = context.GetConfigString("source", "workflow");
        var conversationId = context.GetConfigInt64("conversationId");

        var existing = await _repo.QueryByKeysAsync(context.TenantId, userId, agentId, [memoryKey], cancellationToken);
        var target = existing.FirstOrDefault();
        if (target is null)
        {
            target = new LongTermMemory(
                context.TenantId, userId, agentId,
                Math.Max(0, conversationId), memoryKey, content, source, _idGen.NextId());
            await _repo.AddAsync(target, cancellationToken);
            outputs["created"] = JsonSerializer.SerializeToElement(true);
        }
        else
        {
            target.Reinforce(content, source, Math.Max(0, conversationId));
            await _repo.UpdateAsync(target, cancellationToken);
            outputs["created"] = JsonSerializer.SerializeToElement(false);
        }

        outputs["memory_id"] = JsonSerializer.SerializeToElement(target.Id);
        outputs["memory_key"] = VariableResolver.CreateStringElement(memoryKey);
        return new NodeExecutionResult(true, outputs);
    }
}

/// <summary>
/// 记忆删除节点（M20 P0-3 拆分自 Ltm，MemoryDelete=66）。
/// Config：userId（必填）/ memoryId（删除单条）/ agentId（删除某 agent 范围内全部）。
/// </summary>
public sealed class MemoryDeleteNodeExecutor : INodeExecutor
{
    private readonly IAiMemoryService _service;

    public MemoryDeleteNodeExecutor(IAiMemoryService service)
    {
        _service = service;
    }

    public WorkflowNodeType NodeType => WorkflowNodeType.MemoryDelete;

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var userId = context.GetConfigInt64("userId");
        if (userId <= 0)
        {
            return new NodeExecutionResult(false, outputs, "MemoryDelete 缺少 userId。");
        }

        var memoryId = context.GetConfigInt64("memoryId");
        if (memoryId > 0)
        {
            await _service.DeleteLongTermMemoryAsync(context.TenantId, userId, memoryId, cancellationToken);
            outputs["affected_rows"] = JsonSerializer.SerializeToElement(1);
            outputs["scope"] = VariableResolver.CreateStringElement("single");
            return new NodeExecutionResult(true, outputs);
        }

        var agentId = context.GetConfigInt64("agentId");
        var affected = await _service.ClearLongTermMemoriesAsync(context.TenantId, userId, agentId > 0 ? agentId : null, cancellationToken);
        outputs["affected_rows"] = JsonSerializer.SerializeToElement(affected);
        outputs["scope"] = VariableResolver.CreateStringElement(agentId > 0 ? "agent" : "user");
        return new NodeExecutionResult(true, outputs);
    }
}
