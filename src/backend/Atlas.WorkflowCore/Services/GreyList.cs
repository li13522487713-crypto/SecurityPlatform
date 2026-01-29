using System.Collections.Concurrent;
using Atlas.WorkflowCore.Abstractions;

namespace Atlas.WorkflowCore.Services;

/// <summary>
/// 灰名单管理实现 - 使用内存并发字典
/// </summary>
public class GreyList : IGreyList
{
    private readonly ConcurrentDictionary<string, byte> _items;

    public GreyList()
    {
        _items = new ConcurrentDictionary<string, byte>();
    }

    public void Add(string id)
    {
        _items.TryAdd(id, 0);
    }

    public void Remove(string id)
    {
        _items.TryRemove(id, out _);
    }

    public bool Contains(string id)
    {
        return _items.ContainsKey(id);
    }
}
