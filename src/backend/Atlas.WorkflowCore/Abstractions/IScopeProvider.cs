namespace Atlas.WorkflowCore.Abstractions;

/// <summary>
/// 服务作用域提供者接口 - 为步骤执行创建 DI 作用域
/// </summary>
public interface IScopeProvider
{
    /// <summary>
    /// 创建服务作用域
    /// </summary>
    /// <returns>服务提供者</returns>
    IServiceProvider CreateScope();
}
