using Atlas.Application.LogicFlow.Nodes.Abstractions;
using Atlas.Domain.LogicFlow.Nodes;

namespace Atlas.Infrastructure.LogicFlow.Seeds;

/// <summary>
/// 6 类内置节点种子数据——以 INodeCapabilityDeclaration 形式注册到 INodeTypeRegistry。
/// </summary>
public static class BuiltInNodeSeeds
{
    public static IReadOnlyList<INodeCapabilityDeclaration> All { get; } = BuildAll();

    private static List<INodeCapabilityDeclaration> BuildAll()
    {
        var list = new List<INodeCapabilityDeclaration>();
        list.AddRange(TriggerNodes());
        list.AddRange(DataReadNodes());
        list.AddRange(DataTransformNodes());
        list.AddRange(ControlFlowNodes());
        list.AddRange(TransactionNodes());
        list.AddRange(SystemIntegrationNodes());
        return list;
    }

    private static IEnumerable<INodeCapabilityDeclaration> TriggerNodes()
    {
        yield return new StaticNodeDeclaration("trigger.manual", NodeCategory.Trigger, "手动触发",
            "由用户手动触发逻辑流执行",
            ports: new[]
            {
                Port("out", "输出", PortDirection.Output, PortType.Control),
            },
            caps: Caps(retry: false, timeout: false),
            ui: Ui("rounded", "PlayCircleOutlined", "#52c41a"));

        yield return new StaticNodeDeclaration("trigger.schedule", NodeCategory.Trigger, "定时触发",
            "按 Cron 表达式定时触发",
            ports: new[]
            {
                Port("out", "输出", PortDirection.Output, PortType.Control),
            },
            caps: Caps(retry: false, timeout: true),
            ui: Ui("rounded", "ClockCircleOutlined", "#52c41a"),
            config: ConfigWithFields(new ConfigFieldDefinition
            {
                FieldKey = "cronExpression", DisplayName = "Cron 表达式",
                FieldType = "string", Required = true, Placeholder = "0 0 * * *"
            }));

        yield return new StaticNodeDeclaration("trigger.webhook", NodeCategory.Trigger, "Webhook 触发",
            "接收外部 HTTP 请求触发",
            ports: new[]
            {
                Port("out", "输出", PortDirection.Output, PortType.Data, NodeDataType.Json),
            },
            caps: Caps(retry: false, timeout: false),
            ui: Ui("rounded", "ApiOutlined", "#52c41a"));

        yield return new StaticNodeDeclaration("trigger.event", NodeCategory.Trigger, "事件触发",
            "监听平台内部事件触发",
            ports: new[]
            {
                Port("out", "输出", PortDirection.Output, PortType.Data, NodeDataType.Json),
            },
            caps: Caps(retry: false, timeout: false),
            ui: Ui("rounded", "ThunderboltOutlined", "#52c41a"),
            config: ConfigWithFields(new ConfigFieldDefinition
            {
                FieldKey = "eventType", DisplayName = "事件类型",
                FieldType = "select", Required = true
            }));
    }

    private static IEnumerable<INodeCapabilityDeclaration> DataReadNodes()
    {
        yield return new StaticNodeDeclaration("data.table_query", NodeCategory.DataRead, "表查询",
            "从动态表中查询数据",
            ports: new[]
            {
                Port("in", "输入", PortDirection.Input, PortType.Control),
                Port("out", "数据", PortDirection.Output, PortType.Data, NodeDataType.DatasetHandle),
                Port("err", "错误", PortDirection.Output, PortType.Error),
            },
            caps: Caps(retry: true, timeout: true),
            ui: Ui("rectangle", "TableOutlined", "#1890ff"),
            config: ConfigWithFields(
                new ConfigFieldDefinition { FieldKey = "tableKey", DisplayName = "目标表", FieldType = "select", Required = true },
                new ConfigFieldDefinition { FieldKey = "filterExpression", DisplayName = "过滤条件", FieldType = "expression" }));

        yield return new StaticNodeDeclaration("data.sql_query", NodeCategory.DataRead, "SQL 查询",
            "执行自定义 SQL 查询",
            ports: new[]
            {
                Port("in", "输入", PortDirection.Input, PortType.Control),
                Port("out", "数据", PortDirection.Output, PortType.Data, NodeDataType.DatasetHandle),
                Port("err", "错误", PortDirection.Output, PortType.Error),
            },
            caps: Caps(retry: true, timeout: true),
            ui: Ui("rectangle", "ConsoleSqlOutlined", "#1890ff"),
            config: ConfigWithFields(
                new ConfigFieldDefinition { FieldKey = "sql", DisplayName = "SQL 语句", FieldType = "string", Required = true }));

        yield return new StaticNodeDeclaration("data.api_call", NodeCategory.DataRead, "API 调用",
            "调用外部 API 读取数据",
            ports: new[]
            {
                Port("in", "输入", PortDirection.Input, PortType.Control),
                Port("out", "响应", PortDirection.Output, PortType.Data, NodeDataType.Json),
                Port("err", "错误", PortDirection.Output, PortType.Error),
            },
            caps: Caps(retry: true, timeout: true),
            ui: Ui("rectangle", "CloudDownloadOutlined", "#1890ff"));
    }

    private static IEnumerable<INodeCapabilityDeclaration> DataTransformNodes()
    {
        yield return new StaticNodeDeclaration("transform.field_mapping", NodeCategory.DataTransform, "字段映射",
            "将输入字段映射到输出字段",
            ports: new[]
            {
                Port("in", "输入", PortDirection.Input, PortType.Data, NodeDataType.Record),
                Port("out", "输出", PortDirection.Output, PortType.Data, NodeDataType.Record),
            },
            caps: Caps(),
            ui: Ui("rectangle", "SwapOutlined", "#722ed1"));

        yield return new StaticNodeDeclaration("transform.filter", NodeCategory.DataTransform, "数据过滤",
            "按条件过滤数据集",
            ports: new[]
            {
                Port("in", "输入", PortDirection.Input, PortType.Data, NodeDataType.DatasetHandle),
                Port("out", "输出", PortDirection.Output, PortType.Data, NodeDataType.DatasetHandle),
            },
            caps: Caps(),
            ui: Ui("rectangle", "FilterOutlined", "#722ed1"),
            config: ConfigWithFields(
                new ConfigFieldDefinition { FieldKey = "expression", DisplayName = "过滤表达式", FieldType = "expression", Required = true }));

        yield return new StaticNodeDeclaration("transform.sort", NodeCategory.DataTransform, "数据排序",
            "对数据集排序",
            ports: new[]
            {
                Port("in", "输入", PortDirection.Input, PortType.Data, NodeDataType.DatasetHandle),
                Port("out", "输出", PortDirection.Output, PortType.Data, NodeDataType.DatasetHandle),
            },
            caps: Caps(),
            ui: Ui("rectangle", "SortAscendingOutlined", "#722ed1"));

        yield return new StaticNodeDeclaration("transform.aggregate", NodeCategory.DataTransform, "数据聚合",
            "对数据集执行聚合计算",
            ports: new[]
            {
                Port("in", "输入", PortDirection.Input, PortType.Data, NodeDataType.DatasetHandle),
                Port("out", "输出", PortDirection.Output, PortType.Data, NodeDataType.Record),
            },
            caps: Caps(),
            ui: Ui("rectangle", "CalculatorOutlined", "#722ed1"));

        yield return new StaticNodeDeclaration("transform.expression", NodeCategory.DataTransform, "表达式转换",
            "使用表达式引擎转换字段值",
            ports: new[]
            {
                Port("in", "输入", PortDirection.Input, PortType.Data, NodeDataType.Record),
                Port("out", "输出", PortDirection.Output, PortType.Data, NodeDataType.Record),
            },
            caps: Caps(),
            ui: Ui("rectangle", "FunctionOutlined", "#722ed1"),
            config: ConfigWithFields(
                new ConfigFieldDefinition { FieldKey = "expression", DisplayName = "转换表达式", FieldType = "expression", Required = true }));
    }

    private static IEnumerable<INodeCapabilityDeclaration> ControlFlowNodes()
    {
        yield return new StaticNodeDeclaration("control.condition", NodeCategory.ControlFlow, "条件分支",
            "根据条件判断走不同分支",
            ports: new[]
            {
                Port("in", "输入", PortDirection.Input, PortType.Control),
                Port("true", "是", PortDirection.Output, PortType.Control),
                Port("false", "否", PortDirection.Output, PortType.Control),
            },
            caps: Caps(conditionalBranch: true, maxOut: 2),
            ui: Ui("diamond", "BranchesOutlined", "#fa8c16"),
            config: ConfigWithFields(
                new ConfigFieldDefinition { FieldKey = "expression", DisplayName = "条件表达式", FieldType = "expression", Required = true }));

        yield return new StaticNodeDeclaration("control.switch", NodeCategory.ControlFlow, "多路分支",
            "根据值匹配走不同分支",
            ports: new[]
            {
                Port("in", "输入", PortDirection.Input, PortType.Control),
                Port("default", "默认", PortDirection.Output, PortType.Control),
            },
            caps: Caps(conditionalBranch: true, maxOut: 10),
            ui: Ui("diamond", "ApartmentOutlined", "#fa8c16"));

        yield return new StaticNodeDeclaration("control.foreach", NodeCategory.ControlFlow, "循环",
            "对集合中每个元素执行子流程",
            ports: new[]
            {
                Port("in", "输入", PortDirection.Input, PortType.Data, NodeDataType.Array),
                Port("body", "循环体", PortDirection.Output, PortType.Control),
                Port("done", "完成", PortDirection.Output, PortType.Control),
            },
            caps: Caps(parallel: true, batch: true),
            ui: Ui("rectangle", "RetweetOutlined", "#fa8c16"));

        yield return new StaticNodeDeclaration("control.parallel", NodeCategory.ControlFlow, "并行",
            "并行执行多个分支",
            ports: new[]
            {
                Port("in", "输入", PortDirection.Input, PortType.Control),
                Port("done", "全部完成", PortDirection.Output, PortType.Control),
            },
            caps: Caps(parallel: true, maxOut: 10),
            ui: Ui("rectangle", "NodeExpandOutlined", "#fa8c16"));

        yield return new StaticNodeDeclaration("control.wait", NodeCategory.ControlFlow, "等待",
            "等待指定时间或条件满足后继续",
            ports: new[]
            {
                Port("in", "输入", PortDirection.Input, PortType.Control),
                Port("out", "输出", PortDirection.Output, PortType.Control),
            },
            caps: Caps(timeout: true),
            ui: Ui("rectangle", "HourglassOutlined", "#fa8c16"),
            config: ConfigWithFields(
                new ConfigFieldDefinition { FieldKey = "waitSeconds", DisplayName = "等待秒数", FieldType = "number" }));

        yield return new StaticNodeDeclaration("control.merge", NodeCategory.ControlFlow, "合并",
            "合并多个分支的执行",
            ports: new[]
            {
                Port("in", "输入", PortDirection.Input, PortType.Control, maxConn: 10),
                Port("out", "输出", PortDirection.Output, PortType.Control),
            },
            caps: Caps(maxIn: 10),
            ui: Ui("rectangle", "MergeCellsOutlined", "#fa8c16"));
    }

    private static IEnumerable<INodeCapabilityDeclaration> TransactionNodes()
    {
        yield return new StaticNodeDeclaration("tx.transaction", NodeCategory.Transaction, "事务",
            "在事务上下文中执行子流程",
            ports: new[]
            {
                Port("in", "输入", PortDirection.Input, PortType.Control),
                Port("body", "事务体", PortDirection.Output, PortType.Control),
                Port("done", "完成", PortDirection.Output, PortType.Control),
                Port("err", "错误", PortDirection.Output, PortType.Error),
            },
            caps: Caps(compensation: true),
            ui: Ui("rectangle", "SafetyCertificateOutlined", "#f5222d"));

        yield return new StaticNodeDeclaration("tx.retry", NodeCategory.Transaction, "重试",
            "在失败时按策略重试子节点",
            ports: new[]
            {
                Port("in", "输入", PortDirection.Input, PortType.Control),
                Port("body", "重试体", PortDirection.Output, PortType.Control),
                Port("done", "成功", PortDirection.Output, PortType.Control),
                Port("exhausted", "耗尽", PortDirection.Output, PortType.Error),
            },
            caps: Caps(retry: true),
            ui: Ui("rectangle", "RedoOutlined", "#f5222d"),
            config: ConfigWithFields(
                new ConfigFieldDefinition { FieldKey = "maxAttempts", DisplayName = "最大重试", FieldType = "number", DefaultValue = "3" },
                new ConfigFieldDefinition { FieldKey = "backoffMs", DisplayName = "退避(ms)", FieldType = "number", DefaultValue = "1000" }));

        yield return new StaticNodeDeclaration("tx.compensate", NodeCategory.Transaction, "补偿",
            "执行补偿操作以回滚已完成的步骤",
            ports: new[]
            {
                Port("trigger", "触发", PortDirection.Input, PortType.Compensation),
                Port("body", "补偿体", PortDirection.Output, PortType.Control),
                Port("done", "完成", PortDirection.Output, PortType.Control),
            },
            caps: Caps(compensation: true),
            ui: Ui("rectangle", "RollbackOutlined", "#f5222d"));

        yield return new StaticNodeDeclaration("tx.checkpoint", NodeCategory.Transaction, "检查点",
            "保存执行进度以支持断点恢复",
            ports: new[]
            {
                Port("in", "输入", PortDirection.Input, PortType.Control),
                Port("out", "输出", PortDirection.Output, PortType.Control),
            },
            caps: Caps(),
            ui: Ui("rectangle", "SaveOutlined", "#f5222d"));
    }

    private static IEnumerable<INodeCapabilityDeclaration> SystemIntegrationNodes()
    {
        yield return new StaticNodeDeclaration("sys.http_request", NodeCategory.SystemIntegration, "HTTP 请求",
            "发送 HTTP 请求到外部服务",
            ports: new[]
            {
                Port("in", "输入", PortDirection.Input, PortType.Control),
                Port("out", "响应", PortDirection.Output, PortType.Data, NodeDataType.Json),
                Port("err", "错误", PortDirection.Output, PortType.Error),
            },
            caps: Caps(retry: true, timeout: true),
            ui: Ui("rectangle", "GlobalOutlined", "#13c2c2"),
            config: ConfigWithFields(
                new ConfigFieldDefinition { FieldKey = "url", DisplayName = "URL", FieldType = "string", Required = true },
                new ConfigFieldDefinition { FieldKey = "method", DisplayName = "方法", FieldType = "select", DefaultValue = "GET",
                    Options = new List<string> { "GET", "POST", "PUT", "DELETE", "PATCH" } }));

        yield return new StaticNodeDeclaration("sys.message_send", NodeCategory.SystemIntegration, "消息发送",
            "发送消息到消息队列",
            ports: new[]
            {
                Port("in", "输入", PortDirection.Input, PortType.Control),
                Port("out", "输出", PortDirection.Output, PortType.Control),
                Port("err", "错误", PortDirection.Output, PortType.Error),
            },
            caps: Caps(retry: true),
            ui: Ui("rectangle", "SendOutlined", "#13c2c2"));

        yield return new StaticNodeDeclaration("sys.notification", NodeCategory.SystemIntegration, "通知",
            "发送通知给用户或系统",
            ports: new[]
            {
                Port("in", "输入", PortDirection.Input, PortType.Control),
                Port("out", "输出", PortDirection.Output, PortType.Control),
            },
            caps: Caps(),
            ui: Ui("rectangle", "BellOutlined", "#13c2c2"),
            config: ConfigWithFields(
                new ConfigFieldDefinition { FieldKey = "channel", DisplayName = "通道", FieldType = "select",
                    Options = new List<string> { "email", "sms", "webhook", "in_app" } }));

        yield return new StaticNodeDeclaration("sys.data_sync", NodeCategory.SystemIntegration, "数据同步",
            "同步数据到外部系统",
            ports: new[]
            {
                Port("in", "输入", PortDirection.Input, PortType.Data, NodeDataType.DatasetHandle),
                Port("out", "输出", PortDirection.Output, PortType.Control),
                Port("err", "错误", PortDirection.Output, PortType.Error),
            },
            caps: Caps(retry: true, timeout: true, batch: true),
            ui: Ui("rectangle", "SyncOutlined", "#13c2c2"));
    }

    #region Helpers

    private static PortDefinition Port(string key, string name, PortDirection dir, PortType type,
        NodeDataType dataType = NodeDataType.Any, int maxConn = 1) =>
        new()
        {
            PortKey = key, DisplayName = name, Direction = dir,
            PortType = type, DataType = dataType, MaxConnections = maxConn,
        };

    private static NodeCapability Caps(
        bool retry = false, bool timeout = false, bool compensation = false,
        bool parallel = false, bool batch = false, bool conditionalBranch = false,
        bool subFlow = false, bool breakpoint = true,
        int maxIn = 1, int maxOut = 1) =>
        new()
        {
            SupportsRetry = retry, SupportsTimeout = timeout,
            SupportsCompensation = compensation, SupportsParallelExecution = parallel,
            SupportsBatching = batch, SupportsConditionalBranching = conditionalBranch,
            SupportsSubFlow = subFlow, SupportsBreakpoint = breakpoint,
            MaxInputPorts = maxIn, MaxOutputPorts = maxOut,
        };

    private static NodeUiMetadata Ui(string shape, string icon, string color) =>
        new() { Shape = shape, Icon = icon, Color = color };

    private static NodeConfigSchema ConfigWithFields(params ConfigFieldDefinition[] fields) =>
        new() { Basic = new BasicConfigLayer { Fields = fields.ToList() } };

    #endregion
}

/// <summary>
/// 静态节点能力声明——用于内置节点种子数据，不可变。
/// </summary>
internal sealed class StaticNodeDeclaration : INodeCapabilityDeclaration
{
    private readonly List<PortDefinition> _ports;
    private readonly NodeCapability _caps;
    private readonly NodeConfigSchema _config;
    private readonly NodeUiMetadata _ui;

    public StaticNodeDeclaration(
        string typeKey,
        NodeCategory category,
        string displayName,
        string? description,
        PortDefinition[] ports,
        NodeCapability caps,
        NodeUiMetadata ui,
        NodeConfigSchema? config = null)
    {
        TypeKey = typeKey;
        Category = category;
        DisplayName = displayName;
        Description = description;
        _ports = ports.ToList();
        _caps = caps;
        _config = config ?? new NodeConfigSchema();
        _ui = ui;
    }

    public string TypeKey { get; }
    public NodeCategory Category { get; }
    public string DisplayName { get; }
    public string? Description { get; }

    public NodeCapability GetCapabilities() => _caps;
    public IReadOnlyList<PortDefinition> GetPortDefinitions() => _ports;
    public NodeConfigSchema GetDefaultConfigSchema() => _config;
    public NodeUiMetadata GetUiMetadata() => _ui;
}
