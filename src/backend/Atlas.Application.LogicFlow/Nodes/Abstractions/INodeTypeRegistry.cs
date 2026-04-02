using Atlas.Domain.LogicFlow.Nodes;

namespace Atlas.Application.LogicFlow.Nodes.Abstractions;

/// <summary>
/// 节点类型注册表——管理内置节点能力声明，提供注册、查询与分类能力。
/// </summary>
public interface INodeTypeRegistry
{
    void Register(INodeCapabilityDeclaration declaration);
    INodeCapabilityDeclaration? GetDeclaration(string typeKey);
    IReadOnlyList<INodeCapabilityDeclaration> GetAll();
    IReadOnlyList<INodeCapabilityDeclaration> GetByCategory(NodeCategory category);
    IReadOnlyList<string> GetCategories();
}
