using System.Collections.Concurrent;
using Atlas.Application.LogicFlow.Nodes.Abstractions;
using Atlas.Domain.LogicFlow.Nodes;

namespace Atlas.Infrastructure.LogicFlow.Services;

/// <summary>
/// 内存节点类型注册表——存储所有内置节点的能力声明，线程安全。
/// </summary>
public sealed class NodeTypeRegistry : INodeTypeRegistry
{
    private readonly ConcurrentDictionary<string, INodeCapabilityDeclaration> _declarations = new(StringComparer.OrdinalIgnoreCase);

    public void Register(INodeCapabilityDeclaration declaration)
    {
        _declarations[declaration.TypeKey] = declaration;
    }

    public INodeCapabilityDeclaration? GetDeclaration(string typeKey)
    {
        _declarations.TryGetValue(typeKey, out var declaration);
        return declaration;
    }

    public IReadOnlyList<INodeCapabilityDeclaration> GetAll()
    {
        return _declarations.Values.ToList();
    }

    public IReadOnlyList<INodeCapabilityDeclaration> GetByCategory(NodeCategory category)
    {
        return _declarations.Values
            .Where(d => d.Category == category)
            .ToList();
    }

    public IReadOnlyList<string> GetCategories()
    {
        return _declarations.Values
            .Select(d => d.Category)
            .Distinct()
            .OrderBy(c => c)
            .Select(c => c.ToString())
            .ToList();
    }
}
