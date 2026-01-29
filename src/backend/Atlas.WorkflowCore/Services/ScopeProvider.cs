using Atlas.WorkflowCore.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.WorkflowCore.Services;

/// <summary>
/// 服务作用域提供者实现
/// </summary>
public class ScopeProvider : IScopeProvider
{
    private readonly IServiceProvider _serviceProvider;

    public ScopeProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IServiceScope CreateScope(IStepExecutionContext context)
    {
        return _serviceProvider.CreateScope();
    }
}
