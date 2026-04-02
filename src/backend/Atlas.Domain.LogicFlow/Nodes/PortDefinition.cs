namespace Atlas.Domain.LogicFlow.Nodes;

/// <summary>
/// 节点端口定义（控制/数据/错误/补偿），以 JSON 列存储于 NodeTypeDefinition。
/// </summary>
public sealed class PortDefinition
{
    public string PortKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public PortDirection Direction { get; set; }
    public PortType PortType { get; set; }
    public NodeDataType DataType { get; set; }
    public bool IsRequired { get; set; }
    public int MaxConnections { get; set; } = 1;
    public string? Description { get; set; }
}
