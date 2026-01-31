using Atlas.Core.Tenancy;
using Atlas.WorkflowCore.Abstractions.Persistence;
using Atlas.WorkflowCore.Models;
using Atlas.WorkflowCore.Services.DefaultProviders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;

namespace Atlas.WorkflowCore;

/// <summary>
/// WorkflowCore 配置构建器
/// </summary>
public class WorkflowCoreBuilder
{
    private readonly IServiceCollection _services;
    private bool _hasCustomPersistence;
    private bool _hasCustomTenant;
    private Action<WorkflowOptions>? _configureOptions;

    /// <summary>
    /// 构造函数
    /// </summary>
    public WorkflowCoreBuilder(IServiceCollection services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>
    /// 使用内存持久化提供者（默认）
    /// </summary>
    public WorkflowCoreBuilder UseInMemoryPersistence()
    {
        _services.AddSingleton<IPersistenceProvider, InMemoryPersistenceProvider>();
        _hasCustomPersistence = true;
        return this;
    }

    /// <summary>
    /// 使用自定义持久化提供者
    /// </summary>
    public WorkflowCoreBuilder UsePersistenceProvider<T>() where T : class, IPersistenceProvider
    {
        _services.AddSingleton<IPersistenceProvider, T>();
        _hasCustomPersistence = true;
        return this;
    }

    /// <summary>
    /// 使用自定义租户提供者
    /// </summary>
    public WorkflowCoreBuilder UseTenantProvider<T>() where T : class, ITenantProvider
    {
        _services.AddSingleton<ITenantProvider, T>();
        _hasCustomTenant = true;
        return this;
    }

    /// <summary>
    /// 配置工作流引擎选项
    /// </summary>
    public WorkflowCoreBuilder ConfigureOptions(Action<WorkflowOptions> configure)
    {
        _configureOptions = configure;
        return this;
    }

    /// <summary>
    /// 构建配置并注册默认实现
    /// </summary>
    internal void Build()
    {
        // 1. 注册默认持久化提供者（如果未自定义）
        if (!_hasCustomPersistence)
        {
            _services.AddSingleton<IPersistenceProvider, InMemoryPersistenceProvider>();
        }

        // 2. 注册对象池策略（用于后台任务）
        _services.AddSingleton<IPooledObjectPolicy<IPersistenceProvider>>(sp =>
            new PersistenceProviderPoolPolicy(sp));

        // 3. 注册默认租户提供者（如果未自定义）
        if (!_hasCustomTenant)
        {
            _services.TryAddSingleton<ITenantProvider, DefaultTenantProvider>();
        }

        // 4. 注册 WorkflowOptions 配置
        var options = new WorkflowOptions
        {
            PollInterval = TimeSpan.FromSeconds(1),
            IdleTime = TimeSpan.FromMilliseconds(100),
            EnablePolling = true,
            EnableWorkflows = true,
            EnableEvents = true
        };
        _configureOptions?.Invoke(options);
        _services.AddSingleton(options);
        _services.AddSingleton<IOptions<WorkflowOptions>>(sp => Options.Create(options));
    }
}
