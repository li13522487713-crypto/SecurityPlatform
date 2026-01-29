using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Abstractions.Persistence;
using Microsoft.Extensions.Logging;

namespace Atlas.WorkflowCore.Services;

/// <summary>
/// 工作流主机实现
/// </summary>
public class WorkflowHost : IWorkflowHost
{
    private readonly IWorkflowRegistry _registry;
    private readonly IPersistenceProvider _persistenceProvider;
    private readonly IWorkflowController _controller;
    private readonly ILifeCycleEventHub _lifeCycleEventHub;
    private readonly IActivityController _activityController;
    private readonly IQueueProvider _queueProvider;
    private readonly IDistributedLockProvider _lockProvider;
    private readonly IEnumerable<IBackgroundTask> _backgroundTasks;
    private readonly ILogger<WorkflowHost> _logger;
    private bool _isRunning;

    public WorkflowHost(
        IWorkflowRegistry registry,
        IPersistenceProvider persistenceProvider,
        IWorkflowController controller,
        ILifeCycleEventHub lifeCycleEventHub,
        IActivityController activityController,
        IQueueProvider queueProvider,
        IDistributedLockProvider lockProvider,
        IEnumerable<IBackgroundTask> backgroundTasks,
        ILogger<WorkflowHost> logger)
    {
        _registry = registry;
        _persistenceProvider = persistenceProvider;
        _controller = controller;
        _lifeCycleEventHub = lifeCycleEventHub;
        _activityController = activityController;
        _queueProvider = queueProvider;
        _lockProvider = lockProvider;
        _backgroundTasks = backgroundTasks;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning)
        {
            _logger.LogWarning("工作流主机已经在运行中");
            return;
        }

        _logger.LogInformation("工作流主机启动中...");

        // 1. 确保持久化存储初始化
        await _persistenceProvider.EnsureStoreExists(cancellationToken);
        _logger.LogInformation("持久化存储已初始化");

        // 2. 启动生命周期事件中心
        _lifeCycleEventHub.Start();
        _logger.LogInformation("生命周期事件中心已启动");

        // 3. 启动队列提供者
        await _queueProvider.Start();
        _logger.LogInformation("队列提供者已启动");

        // 4. 启动锁提供者
        await _lockProvider.Start();
        _logger.LogInformation("锁提供者已启动");

        // 5. 启动后台任务
        foreach (var task in _backgroundTasks)
        {
            await task.Start(cancellationToken);
        }
        _logger.LogInformation("后台任务已启动 ({Count} 个)", _backgroundTasks.Count());

        _isRunning = true;
        _logger.LogInformation("工作流主机启动完成");
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRunning)
        {
            _logger.LogWarning("工作流主机未运行");
            return;
        }

        _logger.LogInformation("工作流主机停止中...");

        // 1. 停止后台任务
        foreach (var task in _backgroundTasks)
        {
            await task.Stop();
        }
        _logger.LogInformation("后台任务已停止");

        // 2. 停止锁提供者
        await _lockProvider.Stop();
        _logger.LogInformation("锁提供者已停止");

        // 3. 停止队列提供者
        await _queueProvider.Stop();
        _logger.LogInformation("队列提供者已停止");

        _isRunning = false;
        _logger.LogInformation("工作流主机停止完成");
    }

    public void RegisterWorkflow<TWorkflow>() where TWorkflow : IWorkflow, new()
    {
        var workflow = new TWorkflow();
        _registry.RegisterWorkflow(workflow);
        _logger.LogInformation("已注册工作流: {WorkflowId} v{Version}", workflow.Id, workflow.Version);
    }

    public void RegisterWorkflow<TWorkflow, TData>() where TWorkflow : IWorkflow<TData>, new() where TData : class, new()
    {
        var workflow = new TWorkflow();
        _registry.RegisterWorkflow(workflow);
        _logger.LogInformation("已注册工作流: {WorkflowId} v{Version}", workflow.Id, workflow.Version);
    }

    // 委托 IWorkflowController 方法到 WorkflowController
    public Task<string> StartWorkflowAsync(string workflowId, int? version, object? data, string? reference = null, CancellationToken cancellationToken = default)
        => _controller.StartWorkflowAsync(workflowId, version, data, reference, cancellationToken);

    public Task<string> StartWorkflowAsync<TData>(string workflowId, int? version, TData? data, string? reference = null, CancellationToken cancellationToken = default) where TData : class
        => _controller.StartWorkflowAsync(workflowId, version, data, reference, cancellationToken);

    public Task<bool> SuspendWorkflowAsync(string workflowInstanceId, CancellationToken cancellationToken = default)
        => _controller.SuspendWorkflowAsync(workflowInstanceId, cancellationToken);

    public Task<bool> ResumeWorkflowAsync(string workflowInstanceId, CancellationToken cancellationToken = default)
        => _controller.ResumeWorkflowAsync(workflowInstanceId, cancellationToken);

    public Task<bool> TerminateWorkflowAsync(string workflowInstanceId, CancellationToken cancellationToken = default)
        => _controller.TerminateWorkflowAsync(workflowInstanceId, cancellationToken);

    public Task PublishEventAsync(string eventName, string eventKey, object? eventData = null, CancellationToken cancellationToken = default)
        => _controller.PublishEventAsync(eventName, eventKey, eventData, cancellationToken);
}
