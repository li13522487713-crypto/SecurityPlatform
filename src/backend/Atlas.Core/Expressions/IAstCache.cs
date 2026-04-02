namespace Atlas.Core.Expressions;

/// <summary>
/// AST 编译缓存 —— 将表达式文本的 hash 映射到已解析 AST，避免重复解析。
/// </summary>
public interface IAstCache
{
    bool TryGet(string expression, out ExprAstNode? ast);
    void Set(string expression, ExprAstNode ast);
    void Invalidate(string expression);
    void Clear();
    int Count { get; }
}
