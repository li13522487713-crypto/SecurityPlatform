using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Abstractions.Persistence;
using Atlas.WorkflowCore.Models;
using Atlas.WorkflowCore.Models.LifeCycleEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Atlas.WorkflowCore.Services;

/// <summary>
/// 步骤错误事件处理器委托
/// </summary>
public delegate void StepErrorEventHandler(WorkflowInstance workflow, WorkflowStep step, Exception exception);

/// <summary>
/// 生命周期事件处理器委托
/// </summary>
public delegate void LifeCycleEventHandler(LifeCycleEvent evt);

/// <summary>
/// 工作流主机实现
/// </summary>
public class WorkflowHost : IWorkflowHost, IDisposable
{
    protected bool _shutdown = true;
    protected readonly IServiceProvider _serviceProvider;

    private readonly IEnumerable<IBackgroundTask> _backgroundTasks;
    private readonly IWorkflowController _workflowController;
    private readonly IActivityController _activityController;

    /// <summary>
    /// 步骤错误事件
    /// </summary>
    public event StepErrorEventHandler? OnStepError;

    /// <summary>
    /// 生命周期事件
    /// </summary>
    public event LifeCycleEventHandler? OnLifeCycleEvent;

    // Public dependencies to allow for extension method access.
    public IPersistenceProvider PersistenceStore { get; private set; }
    public IDistributedLockProvider LockProvider { get; private set; }
    public IWorkflowRegistry Registry { get; private set; }
    public WorkflowOptions Options { get; private set; }
    public IQueueProvider QueueProvider { get; private set; }
    public ILogger Logger { get; private set; }

    private readonly ILifeCycleEventHub _lifeCycleEventHub;
    private readonly ISearchIndex _searchIndex;

    public WorkflowHost(
        IPersistenceProvider persistenceStore,
        IQueueProvider queueProvider,
        WorkflowOptions options,
        ILogger<WorkflowHost> logger,
        IServiceProvider serviceProvider,
        IWorkflowRegistry registry,
        IDistributedLockProvider lockProvider,
        IEnumerable<IBackgroundTask> backgroundTasks,
        IWorkflowController workflowController,
        ILifeCycleEventHub lifeCycleEventHub,
        ISearchIndex searchIndex,
        IActivityController activityController)
    {
        PersistenceStore = persistenceStore;
        QueueProvider = queueProvider;
        Options = options;
        Logger = logger;
        _serviceProvider = serviceProvider;
        Registry = registry;
        LockProvider = lockProvider;
        _backgroundTasks = backgroundTasks;
        _workflowController = workflowController;
        _searchIndex = searchIndex;
        _activityController = activityController;
        _lifeCycleEventHub = lifeCycleEventHub;
    }

    public void Start()
    {
        StartAsync(CancellationToken.None).Wait();
    }
    
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var activity = WorkflowActivityTracing.StartHost();
        try
        {
            _shutdown = false;
            await PersistenceStore.EnsureStoreExists(cancellationToken);
            await QueueProvider.Start();
            await LockProvider.Start();
            await _lifeCycleEventHub.Start(cancellationToken);

            // Event subscriptions are removed when stopping the event hub.
            // Add them when starting.
            AddEventSubscriptions();

            Logger.LogInformation("Starting background tasks");

            foreach (var task in _backgroundTasks)
                await task.Start(cancellationToken);
        }
        catch (Exception)
        {
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error);
            throw;
        }
        finally
        {
            activity?.Dispose();
        }
    }

    public void Stop()
    {
        StopAsync(CancellationToken.None).Wait();
    }
    
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _shutdown = true;

        Logger.LogInformation("Stopping background tasks");
        foreach (var th in _backgroundTasks)
            await th.Stop();

        Logger.LogInformation("Worker tasks stopped");

        await QueueProvider.Stop();
        await LockProvider.Stop();
        await _lifeCycleEventHub.Stop();
    }

    public void RegisterWorkflow<TWorkflow>() where TWorkflow : IWorkflow
    {
        var wf = ActivatorUtilities.CreateInstance<TWorkflow>(_serviceProvider);
        Registry.RegisterWorkflow(wf);
    }

    public void RegisterWorkflow<TWorkflow, TData>() where TWorkflow : IWorkflow<TData> where TData : class, new()
    {
        var wf = ActivatorUtilities.CreateInstance<TWorkflow>(_serviceProvider);
        Registry.RegisterWorkflow(wf);
    }

    public Task<string> StartWorkflow(string workflowId, object? data = null, string? reference = null)
    {
        return _workflowController.StartWorkflowAsync(workflowId, null, data, reference);
    }

    public Task<string> StartWorkflow(string workflowId, int? version, object? data = null, string? reference = null)
    {
        return _workflowController.StartWorkflowAsync<object>(workflowId, version, data, reference);
    }

    public Task<string> StartWorkflow<TData>(string workflowId, TData? data = null, string? reference = null)
        where TData : class
    {
        return _workflowController.StartWorkflowAsync<TData>(workflowId, null, data, reference);
    }
    
    public Task<string> StartWorkflow<TData>(string workflowId, int? version, TData? data = null, string? reference = null)
        where TData : class
    {
        return _workflowController.StartWorkflowAsync(workflowId, version, data, reference);
    }

    public Task PublishEvent(string eventName, string eventKey, object? eventData, DateTime? effectiveDate = null)
    {
        return _workflowController.PublishEventAsync(eventName, eventKey, eventData);
    }

    public Task<bool> SuspendWorkflow(string workflowId)
    {
        return _workflowController.SuspendWorkflowAsync(workflowId);
    }

    public Task<bool> ResumeWorkflow(string workflowId)
    {
        return _workflowController.ResumeWorkflowAsync(workflowId);
    }

    public Task<bool> TerminateWorkflow(string workflowId)
    {
        return _workflowController.TerminateWorkflowAsync(workflowId);
    }

    // 委托 IWorkflowController 方法到 WorkflowController（保持兼容性）
    public Task<string> StartWorkflowAsync(string workflowId, int? version, object? data, string? reference = null, CancellationToken cancellationToken = default)
        => _workflowController.StartWorkflowAsync(workflowId, version, data, reference, cancellationToken);

    public Task<string> StartWorkflowAsync<TData>(string workflowId, int? version, TData? data, string? reference = null, CancellationToken cancellationToken = default) where TData : class
        => _workflowController.StartWorkflowAsync(workflowId, version, data, reference, cancellationToken);

    public Task<bool> SuspendWorkflowAsync(string workflowInstanceId, CancellationToken cancellationToken = default)
        => _workflowController.SuspendWorkflowAsync(workflowInstanceId, cancellationToken);

    public Task<bool> ResumeWorkflowAsync(string workflowInstanceId, CancellationToken cancellationToken = default)
        => _workflowController.ResumeWorkflowAsync(workflowInstanceId, cancellationToken);

    public Task<bool> TerminateWorkflowAsync(string workflowInstanceId, CancellationToken cancellationToken = default)
        => _workflowController.TerminateWorkflowAsync(workflowInstanceId, cancellationToken);

    public Task PublishEventAsync(string eventName, string eventKey, object? eventData = null, DateTime? effectiveDate = null, CancellationToken cancellationToken = default)
        => _workflowController.PublishEventAsync(eventName, eventKey, eventData, effectiveDate, cancellationToken);

    public Task<PendingActivity?> GetPendingActivity(string activityName, string workerId, TimeSpan? timeout = null)
    {
        return _activityController.GetPendingActivity(activityName, workerId, timeout);
    }

    public Task ReleaseActivityToken(string token)
    {
        return _activityController.ReleaseActivityToken(token);
    }

    public Task SubmitActivitySuccess(string token, object result)
    {
        return _activityController.SubmitActivitySuccess(token, result);
    }

    public Task SubmitActivityFailure(string token, object result)
    {
        return _activityController.SubmitActivityFailure(token, result);
    }

    /// <summary>
    /// 报告步骤错误
    /// </summary>
    public void ReportStepError(WorkflowInstance workflow, WorkflowStep step, Exception exception)
    {
        try
        {
            OnStepError?.Invoke(workflow, step, exception);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "步骤错误事件处理器执行失败");
        }
    }

    public void HandleLifeCycleEvent(LifeCycleEvent evt)
    {
        OnLifeCycleEvent?.Invoke(evt);
    }

    public void Dispose()
    {
        if (!_shutdown)
            Stop();
    }

    private void AddEventSubscriptions()
    {
        _lifeCycleEventHub.Subscribe<LifeCycleEvent>(HandleLifeCycleEvent);
    }
}
