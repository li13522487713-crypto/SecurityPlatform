using System.Collections.Concurrent;
using Atlas.Core.Expressions;

namespace Atlas.Infrastructure.LogicFlow.Expressions;

/// <summary>
/// AST 编译缓存 —— 使用 ConcurrentDictionary + LRU 淘汰策略。
/// 缓存 key 为表达式原文，value 为已解析 AST。
/// </summary>
public sealed class AstCompilationCache : IAstCache
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly int _maxSize;

    public AstCompilationCache(int maxSize = 4096)
    {
        _maxSize = maxSize;
    }

    public int Count => _cache.Count;

    public bool TryGet(string expression, out ExprAstNode? ast)
    {
        if (_cache.TryGetValue(expression, out var entry))
        {
            entry.LastAccess = Environment.TickCount64;
            ast = entry.Ast;
            return true;
        }
        ast = null;
        return false;
    }

    public void Set(string expression, ExprAstNode ast)
    {
        if (_cache.Count >= _maxSize) Evict();
        _cache[expression] = new CacheEntry { Ast = ast, LastAccess = Environment.TickCount64 };
    }

    public void Invalidate(string expression) => _cache.TryRemove(expression, out _);

    public void Clear() => _cache.Clear();

    private void Evict()
    {
        var toRemove = _cache
            .OrderBy(kv => kv.Value.LastAccess)
            .Take(_cache.Count / 4)
            .Select(kv => kv.Key)
            .ToList();
        foreach (var key in toRemove)
            _cache.TryRemove(key, out _);
    }

    private sealed class CacheEntry
    {
        public required ExprAstNode Ast { get; init; }
        public long LastAccess { get; set; }
    }
}
