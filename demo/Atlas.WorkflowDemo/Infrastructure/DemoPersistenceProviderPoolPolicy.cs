using Atlas.WorkflowCore.Abstractions.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.ObjectPool;

namespace Atlas.WorkflowDemo.Infrastructure;

/// <summary>
/// Demo持久化提供者对象池策略 - 从DI容器获取持久化提供者实例
/// </summary>
public class DemoPersistenceProviderPoolPolicy : IPooledObjectPolicy<IPersistenceProvider>
{
    private readonly IServiceProvider _serviceProvider;

    public DemoPersistenceProviderPoolPolicy(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IPersistenceProvider Create()
    {
        // 从DI容器获取持久化提供者实例
        return _serviceProvider.GetRequiredService<IPersistenceProvider>();
    }

    public bool Return(IPersistenceProvider obj)
    {
        // 单例服务，总是返回true表示可以放回池中
        return true;
    }
}
