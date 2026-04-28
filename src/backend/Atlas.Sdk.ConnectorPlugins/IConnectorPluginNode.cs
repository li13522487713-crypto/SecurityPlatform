namespace Atlas.Sdk.ConnectorPlugins;

/// <summary>
/// External Collaboration Connector 在 Workflow / LogicFlow 引擎中的节点统一接口。
/// 由具体 LogicFlow 编排层把 NodeExecutionRequest 适配成 ConnectorPluginNodeContext，再调用 ExecuteAsync。
/// 这样保留与现有 LogicFlow 节点 SPI 的解耦：Connector 节点不直接依赖 Application.LogicFlow。
/// </summary>
public interface IConnectorPluginNode
{
    /// <summary>节点稳定 type 标识（前端节点目录与服务端 NodeRegistry 共用）。</summary>
    string NodeType { get; }

    string DisplayName { get; }

    string Category { get; }

    Task<ConnectorPluginNodeResult> ExecuteAsync(ConnectorPluginNodeContext context, CancellationToken cancellationToken);
}

public sealed class ConnectorPluginNodeContext
{
    public required Guid TenantId { get; init; }

    /// <summary>provider 实例 ID（外部连接器配置）；部分节点（如 OAuth 绑定）可以为 0。</summary>
    public long ProviderInstanceId { get; init; }

    /// <summary>节点 inputs（已展开的 JSON 路径表达式结果）。</summary>
    public required IReadOnlyDictionary<string, object?> Inputs { get; init; }

    public string TraceId { get; init; } = Guid.NewGuid().ToString("N");
}

public sealed class ConnectorPluginNodeResult
{
    public bool Success { get; init; }

    public IReadOnlyDictionary<string, object?> Outputs { get; init; } = new Dictionary<string, object?>(StringComparer.Ordinal);

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }
}
