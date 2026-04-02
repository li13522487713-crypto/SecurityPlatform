namespace Atlas.Domain.LogicFlow.Nodes;

/// <summary>
/// 节点五层配置模型：基础/绑定/高级/错误/调试。
/// 以 JSON 列存储于 NodeTypeDefinition。
/// </summary>
public sealed class NodeConfigSchema
{
    public BasicConfigLayer? Basic { get; set; }
    public BindingConfigLayer? Binding { get; set; }
    public AdvancedConfigLayer? Advanced { get; set; }
    public ErrorConfigLayer? Error { get; set; }
    public DebugConfigLayer? Debug { get; set; }
}

public sealed class BasicConfigLayer
{
    public List<ConfigFieldDefinition>? Fields { get; set; }
}

public sealed class BindingConfigLayer
{
    public List<BindingDefinition>? InputBindings { get; set; }
    public List<BindingDefinition>? OutputBindings { get; set; }
}

public sealed class AdvancedConfigLayer
{
    public int? MaxRetries { get; set; }
    public int? TimeoutSeconds { get; set; }
    public int? MaxParallelism { get; set; }
    public bool? EnableCache { get; set; }
}

public sealed class ErrorConfigLayer
{
    /// <summary>
    /// Continue / Stop / Retry / Compensate
    /// </summary>
    public string? ErrorStrategy { get; set; }
    public string? FallbackNodeKey { get; set; }
    public bool? EnableErrorPort { get; set; }
}

public sealed class DebugConfigLayer
{
    public bool? EnableBreakpoint { get; set; }
    public bool? LogInput { get; set; }
    public bool? LogOutput { get; set; }
    public string? MockDataJson { get; set; }
}

public sealed class ConfigFieldDefinition
{
    public string FieldKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    /// <summary>
    /// string / number / boolean / select / expression / json
    /// </summary>
    public string FieldType { get; set; } = "string";
    public bool Required { get; set; }
    public string? DefaultValue { get; set; }
    public string? Placeholder { get; set; }
    public List<string>? Options { get; set; }
}

public sealed class BindingDefinition
{
    public string PortKey { get; set; } = string.Empty;
    public string? Expression { get; set; }
    public string? StaticValue { get; set; }
}
