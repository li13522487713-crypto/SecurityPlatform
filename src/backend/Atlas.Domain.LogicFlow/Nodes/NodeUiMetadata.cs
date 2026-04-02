namespace Atlas.Domain.LogicFlow.Nodes;

/// <summary>
/// 节点 UI 渲染元数据：形状/图标/颜色/端口位置。
/// 以 JSON 列存储于 NodeTypeDefinition。
/// </summary>
public sealed class NodeUiMetadata
{
    /// <summary>
    /// rectangle / diamond / circle / rounded / hexagon
    /// </summary>
    public string Shape { get; set; } = "rectangle";
    public string? Icon { get; set; }
    public string? Color { get; set; }
    public string? BackgroundColor { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public List<PortPosition>? PortPositions { get; set; }
}

public sealed class PortPosition
{
    public string PortKey { get; set; } = string.Empty;
    /// <summary>
    /// top / right / bottom / left
    /// </summary>
    public string Side { get; set; } = "left";
    /// <summary>
    /// 0.0 ~ 1.0，沿边的偏移比例。
    /// </summary>
    public double Offset { get; set; } = 0.5;
}
