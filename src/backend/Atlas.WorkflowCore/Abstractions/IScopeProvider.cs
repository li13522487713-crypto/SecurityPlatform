using Microsoft.Extensions.DependencyInjection;

namespace Atlas.WorkflowCore.Abstractions;

/// <summary>
/// 服务作用域提供者接口 - 为步骤执行创建 DI 作用域
/// </summary>
public interface IScopeProvider
{
    /// <summary>
    /// 创建服务作用域
    /// </summary>
    /// <param name="context">步骤执行上下文</param>
    /// <returns>服务作用域（实现IDisposable）</returns>
    IServiceScope CreateScope(IStepExecutionContext context);
}
