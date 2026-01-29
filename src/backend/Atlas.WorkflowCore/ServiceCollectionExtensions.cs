using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.WorkflowCore;

/// <summary>
/// 工作流引擎依赖注入扩展
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加工作流引擎服务
    /// </summary>
    public static IServiceCollection AddWorkflowCore(this IServiceCollection services)
    {
        // 注册核心服务
        services.AddSingleton<IWorkflowRegistry, WorkflowRegistry>();
        services.AddSingleton<ILifeCycleEventHub, LifeCycleEventHub>();
        services.AddSingleton<ILifeCycleEventPublisher>(sp => sp.GetRequiredService<ILifeCycleEventHub>());
        
        // 注册执行引擎
        services.AddSingleton<IStepExecutor, StepExecutor>();
        services.AddSingleton<IWorkflowExecutor, WorkflowExecutor>();
        services.AddSingleton<IWorkflowHost, WorkflowHost>();
        services.AddSingleton<IWorkflowController>(sp => sp.GetRequiredService<IWorkflowHost>());

        return services;
    }
}
