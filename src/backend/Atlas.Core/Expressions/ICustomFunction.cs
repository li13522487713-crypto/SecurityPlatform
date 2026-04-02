namespace Atlas.Core.Expressions;

/// <summary>
/// 自定义函数 SPI —— 插件通过实现此接口向引擎注册扩展函数。
/// </summary>
public interface ICustomFunction
{
    FunctionSignature Signature { get; }
    object? Execute(object?[] arguments);
}
