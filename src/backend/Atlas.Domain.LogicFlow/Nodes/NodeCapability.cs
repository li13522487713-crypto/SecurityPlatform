namespace Atlas.Domain.LogicFlow.Nodes;

/// <summary>
/// 节点能力声明数据，以 JSON 列存储于 NodeTypeDefinition。
/// </summary>
public sealed class NodeCapability
{
    public bool SupportsRetry { get; set; }
    public bool SupportsTimeout { get; set; }
    public bool SupportsCompensation { get; set; }
    public bool SupportsParallelExecution { get; set; }
    public bool SupportsBatching { get; set; }
    public bool SupportsConditionalBranching { get; set; }
    public bool SupportsSubFlow { get; set; }
    public bool SupportsBreakpoint { get; set; }
    public int MaxInputPorts { get; set; } = 1;
    public int MaxOutputPorts { get; set; } = 1;
    public List<string>? RequiredPermissions { get; set; }
}
