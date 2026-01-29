using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Services;
using Atlas.WorkflowCore.Services.BackgroundTasks;
using Atlas.WorkflowCore.Services.DefaultProviders;
using Atlas.WorkflowCore.Services.ErrorHandlers;
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
        // 核心服务
        services.AddSingleton<IWorkflowRegistry, WorkflowRegistry>();
        services.AddSingleton<IWorkflowController, WorkflowController>();
        services.AddSingleton<IWorkflowHost, WorkflowHost>();
        services.AddScoped<IWorkflowExecutor, WorkflowExecutor>();
        services.AddScoped<IStepExecutor, StepExecutor>();

        // 执行结果处理器和指针工厂
        services.AddScoped<IExecutionResultProcessor, ExecutionResultProcessor>();
        services.AddSingleton<IExecutionPointerFactory, ExecutionPointerFactory>();

        // 生命周期事件服务
        services.AddSingleton<ILifeCycleEventHub, LifeCycleEventHub>();
        services.AddSingleton<ILifeCycleEventPublisher, LifeCycleEventHub>(sp => 
            (LifeCycleEventHub)sp.GetRequiredService<ILifeCycleEventHub>());

        // 队列提供者（默认单节点实现）
        services.AddSingleton<IQueueProvider, SingleNodeQueueProvider>();

        // 锁提供者（默认单节点实现）
        services.AddSingleton<IDistributedLockProvider, SingleNodeLockProvider>();

        // 搜索索引（默认空实现）
        services.AddSingleton<ISearchIndex, NullSearchIndex>();

        // 辅助服务
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddSingleton<IScopeProvider, ScopeProvider>();
        services.AddSingleton<IGreyList, GreyList>();

        // 取消处理器
        services.AddScoped<ICancellationProcessor, CancellationProcessor>();

        // 错误处理器
        services.AddSingleton<IWorkflowErrorHandler, RetryHandler>();
        services.AddSingleton<IWorkflowErrorHandler, SuspendHandler>();
        services.AddSingleton<IWorkflowErrorHandler, TerminateHandler>();
        services.AddSingleton<IWorkflowErrorHandler, CompensateHandler>();

        // 活动控制器
        services.AddSingleton<IActivityController, ActivityController>();

        // 同步运行器
        services.AddScoped<ISyncWorkflowRunner, SyncWorkflowRunner>();

        // 中间件运行器
        services.AddScoped<IWorkflowMiddlewareRunner, WorkflowMiddlewareRunner>();
        services.AddSingleton<IWorkflowMiddlewareErrorHandler, DefaultWorkflowMiddlewareErrorHandler>();

        // 后台任务
        services.AddSingleton<IBackgroundTask, WorkflowConsumer>();
        services.AddSingleton<IBackgroundTask, EventConsumer>();
        services.AddSingleton<IBackgroundTask, IndexConsumer>();
        services.AddSingleton<IBackgroundTask, RunnablePoller>();

        return services;
    }
}

