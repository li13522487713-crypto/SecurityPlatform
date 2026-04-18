using System.Text.Json;
using System.Threading.Channels;
using Atlas.Application.LowCode.Abstractions;
using Atlas.Core.Expressions;
using Atlas.Infrastructure.LogicFlow.Expressions;
using Microsoft.Extensions.DependencyInjection;
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
        Channel<SseEvent>? eventChannel,
        long? userId = null,
        string? channelId = null)
    {
        Node = node;
        Variables = variables;
        ServiceProvider = serviceProvider;
        TenantId = tenantId;
        WorkflowId = workflowId;
        ExecutionId = executionId;
        WorkflowCallStack = workflowCallStack;
        EventChannel = eventChannel;
        UserId = userId;
        ChannelId = string.IsNullOrWhiteSpace(channelId) ? null : channelId.Trim();
    }

    public NodeSchema Node { get; }
    public Dictionary<string, JsonElement> Variables { get; }
    public IServiceProvider ServiceProvider { get; }
    public TenantId TenantId { get; }
    public long WorkflowId { get; }
    public long ExecutionId { get; }
    public IReadOnlyList<long> WorkflowCallStack { get; }
    public Channel<SseEvent>? EventChannel { get; }
    /// <summary>D2：执行触发用户 ID（可空——后台/系统流不一定有）。</summary>
    public long? UserId { get; }
    /// <summary>D2：执行渠道（agent/chatflow/app/api 等）。</summary>
    public string? ChannelId { get; }

    /// <summary>
    /// 节点级状态访问器（P3-8 修复 PLAN §M20 S20-7）。
    ///
    /// 此前 INodeStateStore 已实现，但 NodeExecutionContext 缺 State 属性，且无 Executor 真实使用 → 文档与类型分裂。
    /// 现修正为：通过 ServiceProvider 解析 INodeStateStore，按"懒求值 + 4 作用域"暴露给 Executor；
    /// 各有状态节点（SceneVariable / SceneChat / TriggerUpsert / Memory* 等）按需在 ExecuteAsync 中使用：
    ///   await ctx.State.WriteAsync("session", sessionId, ctx.Node.Key, json, ct);
    /// 不持有 INodeStateStore 实例时（如纯单测无 DI），State 调用会抛 BusinessException。
    /// </summary>
    public INodeStateAccessor State => _stateAccessor ??= new NodeStateAccessor(ServiceProvider, TenantId);
    private INodeStateAccessor? _stateAccessor;

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

    public JsonElement EvaluateExpression(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return VariableResolver.ParseLiteral(string.Empty);
        }

        var evaluator = ServiceProvider.GetService<ExprEvaluator>();
        if (evaluator is null)
        {
            return VariableResolver.ParseLiteralOrTemplate(expression, Variables);
        }

        try
        {
            var renderedExpression = VariableResolver.RenderTemplate(expression, Variables);
            var ast = evaluator.ParseAndCache(renderedExpression);
            var context = new ExpressionContext
            {
                Record = BuildExpressionRecord()
            };
            var result = evaluator.Evaluate(ast, context);
            return ToJsonElement(result);
        }
        catch
        {
            return VariableResolver.ParseLiteralOrTemplate(expression, Variables);
        }
    }

    public bool EvaluateCondition(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return true;
        }

        try
        {
            return VariableResolver.IsTruthy(EvaluateExpression(expression));
        }
        catch
        {
            return VariableResolver.EvaluateCondition(expression, Variables);
        }
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

    private Dictionary<string, object?> BuildExpressionRecord()
    {
        var record = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var variable in Variables)
        {
            record[variable.Key] = ConvertJsonElementToClr(variable.Value);
        }

        return record;
    }

    private static object? ConvertJsonElementToClr(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number when value.TryGetInt64(out var integerValue) => integerValue,
            JsonValueKind.Number when value.TryGetDouble(out var numberValue) => numberValue,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Object =>
                value.EnumerateObject().ToDictionary(
                    static x => x.Name,
                    x => ConvertJsonElementToClr(x.Value),
                    StringComparer.OrdinalIgnoreCase),
            JsonValueKind.Array =>
                value.EnumerateArray().Select(ConvertJsonElementToClr).ToList(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => null
        };
    }

    private static JsonElement ToJsonElement(object? value)
    {
        if (value is null)
        {
            return ParseNullElement();
        }

        if (value is JsonElement element)
        {
            return element;
        }

        using var document = JsonSerializer.SerializeToDocument(value);
        return document.RootElement.Clone();
    }

    private static JsonElement ParseNullElement()
    {
        using var document = JsonDocument.Parse("null");
        return document.RootElement.Clone();
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

/// <summary>
/// 节点级状态访问器（P3-8）：在 NodeExecutionContext 内对 INodeStateStore 做最小封装，固定租户上下文。
/// 4 作用域（与 docs/lowcode-orchestration-spec.md §3 对齐）：
///  - session：会话级（key=sessionId）
///  - conversation：对话级（key=conversationId）
///  - trigger：触发器级（key=triggerId）
///  - app：应用级（key=appId）
/// nodeKey 默认取自 NodeExecutionContext.Node.Key，避免不同节点污染同 key。
/// </summary>
public interface INodeStateAccessor
{
    Task<string?> ReadAsync(string scope, string scopeKey, CancellationToken cancellationToken);
    Task<string?> ReadAsync(string scope, string scopeKey, string nodeKey, CancellationToken cancellationToken);
    Task WriteAsync(string scope, string scopeKey, string stateJson, CancellationToken cancellationToken);
    Task WriteAsync(string scope, string scopeKey, string nodeKey, string stateJson, CancellationToken cancellationToken);
    Task DeleteAsync(string scope, string scopeKey, CancellationToken cancellationToken);
    Task DeleteAsync(string scope, string scopeKey, string nodeKey, CancellationToken cancellationToken);
}

/// <summary>
/// INodeStateAccessor 默认实现：通过 IServiceProvider 解析 INodeStateStore；
/// 当 ServiceProvider 中不含 INodeStateStore 时（如单测构造的最小注入），调用方法会抛 InvalidOperationException。
/// </summary>
internal sealed class NodeStateAccessor : INodeStateAccessor
{
    private readonly IServiceProvider _provider;
    private readonly TenantId _tenantId;

    public NodeStateAccessor(IServiceProvider provider, TenantId tenantId)
    {
        _provider = provider;
        _tenantId = tenantId;
    }

    private INodeStateStore Resolve()
    {
        var s = _provider.GetService<INodeStateStore>();
        if (s is null)
        {
            throw new InvalidOperationException(
                "INodeStateStore 未在 DI 注册；请确认 LowCodeServiceRegistration.AddLowCodeInfrastructure 已调用。");
        }
        return s;
    }

    public Task<string?> ReadAsync(string scope, string scopeKey, CancellationToken cancellationToken)
        => ReadAsync(scope, scopeKey, NodeKeyFallback, cancellationToken);

    public Task<string?> ReadAsync(string scope, string scopeKey, string nodeKey, CancellationToken cancellationToken)
        => Resolve().ReadAsync(_tenantId, scope, scopeKey, nodeKey, cancellationToken);

    public Task WriteAsync(string scope, string scopeKey, string stateJson, CancellationToken cancellationToken)
        => WriteAsync(scope, scopeKey, NodeKeyFallback, stateJson, cancellationToken);

    public Task WriteAsync(string scope, string scopeKey, string nodeKey, string stateJson, CancellationToken cancellationToken)
        => Resolve().WriteAsync(_tenantId, scope, scopeKey, nodeKey, stateJson, cancellationToken);

    public Task DeleteAsync(string scope, string scopeKey, CancellationToken cancellationToken)
        => DeleteAsync(scope, scopeKey, NodeKeyFallback, cancellationToken);

    public Task DeleteAsync(string scope, string scopeKey, string nodeKey, CancellationToken cancellationToken)
        => Resolve().DeleteAsync(_tenantId, scope, scopeKey, nodeKey, cancellationToken);

    /// <summary>
    /// nodeKey 兜底：当调用方没有提供 nodeKey 时（短调用），用 "default" 作为默认。
    /// 如果该 Executor 需要按 NodeSchema.Key 隔离，应显式传入。
    /// </summary>
    private const string NodeKeyFallback = "default";
}
