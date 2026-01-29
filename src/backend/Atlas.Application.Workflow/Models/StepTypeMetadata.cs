namespace Atlas.Application.Workflow.Models;

/// <summary>
/// 工作流步骤类型元数据
/// </summary>
public class StepTypeMetadata
{
    /// <summary>
    /// 类型名称（如：Delay, If, While, Foreach）
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 显示名称（如：延迟、条件判断、循环、遍历）
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// 分类（如：控制流、时间控制、容器）
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// 前端显示颜色（十六进制格式）
    /// </summary>
    public string Color { get; set; } = string.Empty;

    /// <summary>
    /// 图标标识
    /// </summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// 参数定义列表
    /// </summary>
    public List<StepParameter> Parameters { get; set; } = new();
}

/// <summary>
/// 步骤参数定义
/// </summary>
public class StepParameter
{
    /// <summary>
    /// 参数名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 参数类型（string, bool, int, timespan, datetime, array）
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 是否必需
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// 默认值
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// 参数描述
    /// </summary>
    public string Description { get; set; } = string.Empty;
}
