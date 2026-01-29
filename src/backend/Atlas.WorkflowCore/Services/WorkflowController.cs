using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Abstractions.Persistence;
using Atlas.WorkflowCore.Models;
using Atlas.WorkflowCore.Models.LifeCycleEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Atlas.WorkflowCore.Services;

/// <summary>
/// 工作流控制器实现 - 核心业务逻辑层
/// </summary>
public class WorkflowController : IWorkflowController
{
    private readonly IWorkflowRegistry _registry;
    private readonly IPersistenceProvider _persistenceProvider;
    private readonly ILifeCycleEventPublisher _eventPublisher;
    private readonly IDistributedLockProvider _lockProvider;
    private readonly IQueueProvider _queueProvider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IExecutionPointerFactory _pointerFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<WorkflowController> _logger;

    public WorkflowController(
        IWorkflowRegistry registry,
        IPersistenceProvider persistenceProvider,
        ILifeCycleEventPublisher eventPublisher,
        IDistributedLockProvider lockProvider,
        IQueueProvider queueProvider,
        IServiceScopeFactory scopeFactory,
        IExecutionPointerFactory pointerFactory,
        IServiceProvider serviceProvider,
        IDateTimeProvider dateTimeProvider,
        ILogger<WorkflowController> logger)
    {
        _registry = registry;
        _persistenceProvider = persistenceProvider;
        _eventPublisher = eventPublisher;
        _lockProvider = lockProvider;
        _queueProvider = queueProvider;
        _scopeFactory = scopeFactory;
        _pointerFactory = pointerFactory;
        _serviceProvider = serviceProvider;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<string> StartWorkflowAsync(string workflowId, int? version, object? data, string? reference = null, CancellationToken cancellationToken = default)
    {
        var definition = _registry.GetDefinition(workflowId, version);
        if (definition == null)
        {
            throw new InvalidOperationException($"工作流定义不存在: {workflowId} v{version}");
        }

        // 如果 data 为 null 但工作流定义了 DataType，则自动创建实例
        if (data == null && definition.DataType != null)
        {
            data = Activator.CreateInstance(definition.DataType);
        }

        // 创建工作流实例
        var instance = new WorkflowInstance
        {
            Id = Guid.NewGuid().ToString(),
            WorkflowDefinitionId = definition.Id,
            Version = definition.Version,
            Status = WorkflowStatus.Runnable,
            Data = data != null ? JsonSerializer.Serialize(data) : null,
            Reference = reference,
            CreateTime = _dateTimeProvider.UtcNow,
            NextExecution = new DateTimeOffset(_dateTimeProvider.UtcNow).ToUnixTimeMilliseconds()
        };

        // 初始化执行指针
        var initialPointer = _pointerFactory.BuildGenesisPointer(definition);
        instance.ExecutionPointers.Add(initialPointer);

        // 运行 PreWorkflow 中间件
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var middlewareRunner = scope.ServiceProvider.GetRequiredService<IWorkflowMiddlewareRunner>();
            await middlewareRunner.RunPreMiddleware(instance, definition);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PreWorkflow 中间件执行失败");
            throw;
        }

        // 持久化工作流实例
        var instanceId = await _persistenceProvider.CreateWorkflowAsync(instance, cancellationToken);

        // 将实例加入可运行队列
        await _queueProvider.QueueWork(instanceId, QueueType.Workflow);
        await _queueProvider.QueueWork(instanceId, QueueType.Index);

        // 发布启动事件
        _eventPublisher.PublishNotification(new WorkflowStarted
        {
            EventTimeUtc = _dateTimeProvider.UtcNow,
            WorkflowInstanceId = instanceId,
            WorkflowDefinitionId = definition.Id,
            Version = definition.Version,
            Reference = reference
        });

        _logger.LogInformation("工作流实例已启动: {InstanceId} ({WorkflowId} v{Version})", instanceId, definition.Id, definition.Version);

        return instanceId;
    }

    public async Task<string> StartWorkflowAsync<TData>(string workflowId, int? version, TData? data, string? reference = null, CancellationToken cancellationToken = default) where TData : class
    {
        var definition = _registry.GetDefinition(workflowId, version);
        if (definition == null)
        {
            throw new InvalidOperationException($"工作流定义不存在: {workflowId} v{version}");
        }

        object? workflowData = data;
        
        // 如果 data 为 null 但工作流定义了 DataType，则自动创建实例
        if (workflowData == null && definition.DataType != null)
        {
            if (typeof(TData) == definition.DataType)
                workflowData = Activator.CreateInstance<TData>();
            else
                workflowData = Activator.CreateInstance(definition.DataType);
        }

        return await StartWorkflowAsync(workflowId, version, workflowData, reference, cancellationToken);
    }

    public async Task<bool> SuspendWorkflowAsync(string workflowInstanceId, CancellationToken cancellationToken = default)
    {
        // 使用分布式锁保护
        var lockKey = $"workflow:{workflowInstanceId}";
        if (!await _lockProvider.AcquireLock(lockKey, cancellationToken))
        {
            _logger.LogWarning("无法获取工作流锁: {InstanceId}", workflowInstanceId);
            return false;
        }

        try
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
            await _queueProvider.QueueWork(workflowInstanceId, QueueType.Index);

            // 发布挂起事件
            _eventPublisher.PublishNotification(new WorkflowSuspended
            {
                EventTimeUtc = _dateTimeProvider.UtcNow,
                WorkflowInstanceId = instance.Id,
                WorkflowDefinitionId = instance.WorkflowDefinitionId,
                Version = instance.Version,
                Reference = instance.Reference
            });

            _logger.LogInformation("工作流实例已挂起: {InstanceId}", workflowInstanceId);
            return true;
        }
        finally
        {
            await _lockProvider.ReleaseLock(lockKey);
        }
    }

    public async Task<bool> ResumeWorkflowAsync(string workflowInstanceId, CancellationToken cancellationToken = default)
    {
        // 使用分布式锁保护
        var lockKey = $"workflow:{workflowInstanceId}";
        if (!await _lockProvider.AcquireLock(lockKey, cancellationToken))
        {
            _logger.LogWarning("无法获取工作流锁: {InstanceId}", workflowInstanceId);
            return false;
        }

        try
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
            instance.NextExecution = new DateTimeOffset(_dateTimeProvider.UtcNow).ToUnixTimeMilliseconds();
            await _persistenceProvider.PersistWorkflowAsync(instance, cancellationToken);

            // 重新加入可运行队列
            await _queueProvider.QueueWork(workflowInstanceId, QueueType.Workflow);
            await _queueProvider.QueueWork(workflowInstanceId, QueueType.Index);

            // 发布恢复事件
            _eventPublisher.PublishNotification(new WorkflowResumed
            {
                EventTimeUtc = _dateTimeProvider.UtcNow,
                WorkflowInstanceId = instance.Id,
                WorkflowDefinitionId = instance.WorkflowDefinitionId,
                Version = instance.Version,
                Reference = instance.Reference
            });

            _logger.LogInformation("工作流实例已恢复: {InstanceId}", workflowInstanceId);
            return true;
        }
        finally
        {
            await _lockProvider.ReleaseLock(lockKey);
        }
    }

    public async Task<bool> TerminateWorkflowAsync(string workflowInstanceId, CancellationToken cancellationToken = default)
    {
        // 使用分布式锁保护
        var lockKey = $"workflow:{workflowInstanceId}";
        if (!await _lockProvider.AcquireLock(lockKey, cancellationToken))
        {
            _logger.LogWarning("无法获取工作流锁: {InstanceId}", workflowInstanceId);
            return false;
        }

        try
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
            await _queueProvider.QueueWork(workflowInstanceId, QueueType.Index);

            // 发布终止事件
            _eventPublisher.PublishNotification(new WorkflowTerminated
            {
                EventTimeUtc = _dateTimeProvider.UtcNow,
                WorkflowInstanceId = instance.Id,
                WorkflowDefinitionId = instance.WorkflowDefinitionId,
                Version = instance.Version,
                Reference = instance.Reference
            });

            _logger.LogInformation("工作流实例已终止: {InstanceId}", workflowInstanceId);
            return true;
        }
        finally
        {
            await _lockProvider.ReleaseLock(lockKey);
        }
    }

    public async Task PublishEventAsync(string eventName, string eventKey, object? eventData = null, DateTime? effectiveDate = null, CancellationToken cancellationToken = default)
    {
        var evt = new Event
        {
            Id = Guid.NewGuid().ToString(),
            EventName = eventName,
            EventKey = eventKey,
            EventData = eventData,
            EventTime = effectiveDate.HasValue ? effectiveDate.Value.ToUniversalTime() : _dateTimeProvider.UtcNow,
            IsProcessed = false
        };

        await _persistenceProvider.CreateEventAsync(evt, cancellationToken);

        // 将事件加入处理队列
        await _queueProvider.QueueWork(evt.Id, QueueType.Event);

        _logger.LogInformation("外部事件已发布: {EventName}#{EventKey}", eventName, eventKey);
    }
}
