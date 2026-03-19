using System.Text.Json;
using System.Threading.Channels;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Domain.AiPlatform.ValueObjects;

namespace Atlas.Infrastructure.Services.WorkflowEngine;

/// <summary>
/// V2 工作流节点执行器接口。每种 <see cref="WorkflowNodeType"/> 对应一个实现。
/// </summary>
public interface INodeExecutor
{
    WorkflowNodeType NodeType { get; }

    Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken);
}

/// <summary>
/// 节点执行上下文——由 DagExecutor 为每个节点创建。
/// </summary>
public sealed class NodeExecutionContext
{
    public NodeExecutionContext(
        NodeSchema node,
        Dictionary<string, JsonElement> variables,
        IServiceProvider serviceProvider,
        TenantId tenantId,
        long workflowId,
        long executionId,
        IReadOnlyList<long> workflowCallStack,
        Channel<SseEvent>? eventChannel)
    {
        Node = node;
        Variables = variables;
        ServiceProvider = serviceProvider;
        TenantId = tenantId;
        WorkflowId = workflowId;
        ExecutionId = executionId;
        WorkflowCallStack = workflowCallStack;
        EventChannel = eventChannel;
    }

    public NodeSchema Node { get; }
    public Dictionary<string, JsonElement> Variables { get; }
    public IServiceProvider ServiceProvider { get; }
    public TenantId TenantId { get; }
    public long WorkflowId { get; }
    public long ExecutionId { get; }
    public IReadOnlyList<long> WorkflowCallStack { get; }
    public Channel<SseEvent>? EventChannel { get; }

    /// <summary>
    /// 向 SSE 事件通道写入一条事件（如果通道可用）。
    /// </summary>
    public async ValueTask EmitEventAsync(string eventType, string data, CancellationToken cancellationToken)
    {
        if (EventChannel is not null)
        {
            await EventChannel.Writer.WriteAsync(new SseEvent(eventType, data), cancellationToken);
        }
    }

    /// <summary>
    /// 将模板中的 {{key}} 占位符替换为变量值（忽略大小写）。
    /// </summary>
    public string ReplaceVariables(string template)
    {
        return VariableResolver.RenderTemplate(template, Variables);
    }

    public JsonElement ParseLiteralOrTemplate(string value)
    {
        return VariableResolver.ParseLiteralOrTemplate(value, Variables);
    }

    public bool EvaluateCondition(string expression)
    {
        return VariableResolver.EvaluateCondition(expression, Variables);
    }

    public string GetConfigString(string key, string defaultValue = "")
    {
        return VariableResolver.GetConfigString(Node.Config, key, defaultValue);
    }

    public int GetConfigInt32(string key, int defaultValue = 0)
    {
        return VariableResolver.GetConfigInt32(Node.Config, key, defaultValue);
    }

    public long GetConfigInt64(string key, long defaultValue = 0L)
    {
        return VariableResolver.GetConfigInt64(Node.Config, key, defaultValue);
    }

    public bool GetConfigBoolean(string key, bool defaultValue = false)
    {
        return VariableResolver.GetConfigBoolean(Node.Config, key, defaultValue);
    }

    public bool TryResolveVariable(string path, out JsonElement value)
    {
        return VariableResolver.TryResolvePath(Variables, path, out value);
    }
}

/// <summary>
/// 节点执行结果。
/// </summary>
public sealed record NodeExecutionResult(
    bool Success,
    Dictionary<string, JsonElement> Outputs,
    string? ErrorMessage = null,
    InterruptType InterruptType = InterruptType.None);

/// <summary>
/// 节点类型元数据——供 NodeExecutorRegistry 对外暴露。
/// </summary>
public sealed record NodeTypeMetadata(
    WorkflowNodeType Type,
    string Key,
    string Name,
    string Category,
    string Description);
