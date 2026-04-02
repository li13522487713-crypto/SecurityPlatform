using Atlas.Domain.LogicFlow.Nodes;

namespace Atlas.Application.LogicFlow.Nodes.Abstractions;

/// <summary>
/// 节点能力声明接口——每种内置节点类型实现此接口，以编程方式声明端口、配置与能力。
/// </summary>
public interface INodeCapabilityDeclaration
{
    string TypeKey { get; }
    NodeCategory Category { get; }
    string DisplayName { get; }
    string? Description { get; }
    NodeCapability GetCapabilities();
    IReadOnlyList<PortDefinition> GetPortDefinitions();
    NodeConfigSchema GetDefaultConfigSchema();
    NodeUiMetadata GetUiMetadata();
}
