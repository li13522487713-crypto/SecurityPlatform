using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Abstractions.Persistence;
using Atlas.WorkflowCore.Models;
using Atlas.WorkflowCore.Models.LifeCycleEvents;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Atlas.WorkflowCore.Services;

/// <summary>
/// 工作流主机实现
/// </summary>
public class WorkflowHost : IWorkflowHost
{
    private readonly IWorkflowRegistry _registry;
    private readonly IPersistenceProvider _persistenceProvider;
    private readonly IWorkflowExecutor _executor;
    private readonly ILifeCycleEventPublisher _eventPublisher;
    private readonly ILogger<WorkflowHost> _logger;
    private bool _isRunning;

    public WorkflowHost(
        IWorkflowRegistry registry,
        IPersistenceProvider persistenceProvider,
        IWorkflowExecutor executor,
        ILifeCycleEventPublisher eventPublisher,
        ILogger<WorkflowHost> logger)
    {
        _registry = registry;
        _persistenceProvider = persistenceProvider;
        _executor = executor;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning)
        {
            _logger.LogWarning("工作流主机已经在运行中");
            return Task.CompletedTask;
        }

        _logger.LogInformation("工作流主机启动");
        _isRunning = true;

        // TODO: 启动后台任务轮询可运行的工作流实例
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRunning)
        {
            _logger.LogWarning("工作流主机未运行");
            return Task.CompletedTask;
        }

        _logger.LogInformation("工作流主机停止");
        _isRunning = false;

        // TODO: 停止后台任务
        return Task.CompletedTask;
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

    public async Task<string> StartWorkflowAsync(string workflowId, int? version, object? data, string? reference = null, CancellationToken cancellationToken = default)
    {
        var definition = _registry.GetDefinition(workflowId, version);
        if (definition == null)
        {
            throw new InvalidOperationException($"工作流定义不存在: {workflowId} v{version}");
        }

        var instance = new WorkflowInstance
        {
            Id = Guid.NewGuid().ToString(),
            WorkflowDefinitionId = definition.Id,
            Version = definition.Version,
            Status = WorkflowStatus.Runnable,
            Data = data != null ? JsonSerializer.Serialize(data) : null,
            Reference = reference,
            CreateTime = DateTime.UtcNow,
            NextExecution = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        // 初始化执行指针
        var startStep = definition.Steps.FirstOrDefault(s => s.Id == 0);
        if (startStep != null)
        {
            instance.ExecutionPointers.Add(new ExecutionPointer
            {
                Id = Guid.NewGuid().ToString(),
                StepId = startStep.Id,
                StepName = startStep.Name,
                Status = PointerStatus.Pending,
                Active = true
            });
        }

        var instanceId = await _persistenceProvider.CreateWorkflowAsync(instance, cancellationToken);

        // 发布启动事件
        await _eventPublisher.PublishNotificationAsync(new WorkflowStarted
        {
            WorkflowInstanceId = instanceId,
            WorkflowDefinitionId = definition.Id,
            Version = definition.Version,
            Reference = instanceId
        }, cancellationToken);

        _logger.LogInformation("工作流实例已启动: {InstanceId} ({WorkflowId} v{Version})", instanceId, definition.Id, definition.Version);

        return instanceId;
    }

    public async Task<string> StartWorkflowAsync<TData>(string workflowId, int? version, TData? data, string? reference = null, CancellationToken cancellationToken = default) where TData : class
    {
        return await StartWorkflowAsync(workflowId, version, (object?)data, reference, cancellationToken);
    }

    public async Task<bool> SuspendWorkflowAsync(string workflowInstanceId, CancellationToken cancellationToken = default)
    {
        var instance = await _persistenceProvider.GetWorkflowAsync(workflowInstanceId, cancellationToken);
        if (instance == null)
        {
            _logger.LogWarning("工作流实例不存在: {InstanceId}", workflowInstanceId);
            return false;
        }

        if (instance.Status != WorkflowStatus.Runnable)
        {
            _logger.LogWarning("工作流实例状态不可挂起: {InstanceId} (状态: {Status})", workflowInstanceId, instance.Status);
            return false;
        }

        instance.Status = WorkflowStatus.Suspended;
        await _persistenceProvider.PersistWorkflowAsync(instance, cancellationToken);

        // 发布挂起事件
        await _eventPublisher.PublishNotificationAsync(new WorkflowSuspended
        {
            WorkflowInstanceId = instance.Id,
            WorkflowDefinitionId = instance.WorkflowDefinitionId,
            Version = instance.Version,
            Reference = instance.Id
        }, cancellationToken);

        _logger.LogInformation("工作流实例已挂起: {InstanceId}", workflowInstanceId);
        return true;
    }

    public async Task<bool> ResumeWorkflowAsync(string workflowInstanceId, CancellationToken cancellationToken = default)
    {
        var instance = await _persistenceProvider.GetWorkflowAsync(workflowInstanceId, cancellationToken);
        if (instance == null)
        {
            _logger.LogWarning("工作流实例不存在: {InstanceId}", workflowInstanceId);
            return false;
        }

        if (instance.Status != WorkflowStatus.Suspended)
        {
            _logger.LogWarning("工作流实例状态不可恢复: {InstanceId} (状态: {Status})", workflowInstanceId, instance.Status);
            return false;
        }

        instance.Status = WorkflowStatus.Runnable;
        instance.NextExecution = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await _persistenceProvider.PersistWorkflowAsync(instance, cancellationToken);

        // 发布恢复事件
        await _eventPublisher.PublishNotificationAsync(new WorkflowResumed
        {
            WorkflowInstanceId = instance.Id,
            WorkflowDefinitionId = instance.WorkflowDefinitionId,
            Version = instance.Version,
            Reference = instance.Id
        }, cancellationToken);

        _logger.LogInformation("工作流实例已恢复: {InstanceId}", workflowInstanceId);
        return true;
    }

    public async Task<bool> TerminateWorkflowAsync(string workflowInstanceId, CancellationToken cancellationToken = default)
    {
        var instance = await _persistenceProvider.GetWorkflowAsync(workflowInstanceId, cancellationToken);
        if (instance == null)
        {
            _logger.LogWarning("工作流实例不存在: {InstanceId}", workflowInstanceId);
            return false;
        }

        if (instance.Status == WorkflowStatus.Complete || instance.Status == WorkflowStatus.Terminated)
        {
            _logger.LogWarning("工作流实例已终止: {InstanceId} (状态: {Status})", workflowInstanceId, instance.Status);
            return false;
        }

        await _persistenceProvider.TerminateWorkflowAsync(workflowInstanceId, cancellationToken);

        // 发布终止事件
        await _eventPublisher.PublishNotificationAsync(new WorkflowTerminated
        {
            WorkflowInstanceId = instance.Id,
            WorkflowDefinitionId = instance.WorkflowDefinitionId,
            Version = instance.Version,
            Reference = instance.Id
        }, cancellationToken);

        _logger.LogInformation("工作流实例已终止: {InstanceId}", workflowInstanceId);
        return true;
    }

    public async Task PublishEventAsync(string eventName, string eventKey, object? eventData = null, CancellationToken cancellationToken = default)
    {
        var evt = new Event
        {
            Id = Guid.NewGuid().ToString(),
            EventName = eventName,
            EventKey = eventKey,
            EventData = eventData,
            EventTime = DateTime.UtcNow,
            IsProcessed = false
        };

        await _persistenceProvider.CreateEventAsync(evt, cancellationToken);
        _logger.LogInformation("外部事件已发布: {EventName}#{EventKey}", eventName, eventKey);
    }
}
