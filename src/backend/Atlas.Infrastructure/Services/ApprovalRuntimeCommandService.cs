using AutoMapper;
using System.Text.Json;
using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Models;
using Atlas.Application.Approval.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;
using Atlas.Infrastructure.Services.ApprovalFlow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 审批流运行时命令服务实现（核心引擎逻辑）
/// </summary>
public sealed class ApprovalRuntimeCommandService : IApprovalRuntimeCommandService
{
    private readonly IApprovalFlowRepository _flowRepository;
    private readonly IApprovalInstanceRepository _instanceRepository;
    private readonly IApprovalTaskRepository _taskRepository;
    private readonly IApprovalHistoryRepository _historyRepository;
    private readonly IApprovalDepartmentLeaderRepository _deptLeaderRepository;
    private readonly IApprovalNodeExecutionRepository _nodeExecutionRepository;
    private readonly IApprovalParallelTokenRepository _parallelTokenRepository;
    private readonly IApprovalCopyRecordRepository _copyRecordRepository;
    private readonly IApprovalProcessVariableRepository _processVariableRepository;
    private readonly IApprovalNotificationService? _notificationService;
    private readonly IApprovalTimeoutReminderRepository? _timeoutReminderRepository;
    private readonly ExternalCallbackService? _callbackService;
    private readonly ApprovalStatusSyncHandler? _statusSyncHandler;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IBackgroundWorkQueue? _backgroundWorkQueue;
    private readonly IUnitOfWork? _unitOfWork;
    private readonly IMapper _mapper;
    private readonly FlowEngine _flowEngine;
    private readonly ILogger<ApprovalRuntimeCommandService>? _logger;

    public ApprovalRuntimeCommandService(
        IApprovalFlowRepository flowRepository,
        IApprovalInstanceRepository instanceRepository,
        IApprovalTaskRepository taskRepository,
        IApprovalHistoryRepository historyRepository,
        IApprovalDepartmentLeaderRepository deptLeaderRepository,
        IApprovalNodeExecutionRepository nodeExecutionRepository,
        IApprovalParallelTokenRepository parallelTokenRepository,
        IApprovalCopyRecordRepository copyRecordRepository,
        IApprovalProcessVariableRepository processVariableRepository,
        IApprovalUserQueryService userQueryService,
        IIdGeneratorAccessor idGeneratorAccessor,
        IMapper mapper,
        IUnitOfWork? unitOfWork = null,
        IApprovalNotificationService? notificationService = null,
        IApprovalTimeoutReminderRepository? timeoutReminderRepository = null,
        ExternalCallbackService? callbackService = null,
        ApprovalStatusSyncHandler? statusSyncHandler = null,
        IBackgroundWorkQueue? backgroundWorkQueue = null,
        ILogger<ApprovalRuntimeCommandService>? logger = null)
    {
        _flowRepository = flowRepository;
        _instanceRepository = instanceRepository;
        _taskRepository = taskRepository;
        _historyRepository = historyRepository;
        _deptLeaderRepository = deptLeaderRepository;
        _nodeExecutionRepository = nodeExecutionRepository;
        _parallelTokenRepository = parallelTokenRepository;
        _copyRecordRepository = copyRecordRepository;
        _processVariableRepository = processVariableRepository;
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _timeoutReminderRepository = timeoutReminderRepository;
        _callbackService = callbackService;
        _statusSyncHandler = statusSyncHandler;
        _idGeneratorAccessor = idGeneratorAccessor;
        _backgroundWorkQueue = backgroundWorkQueue;
        _mapper = mapper;
        _logger = logger;
        var conditionEvaluator = new ConditionEvaluator(processVariableRepository);
        var deduplicationService = new DeduplicationService(taskRepository, userQueryService);
        _flowEngine = new FlowEngine(
            taskRepository,
            nodeExecutionRepository,
            deptLeaderRepository,
            parallelTokenRepository,
            copyRecordRepository,
            conditionEvaluator,
            userQueryService,
            deduplicationService,
            idGeneratorAccessor,
            notificationService,
            timeoutReminderRepository,
            callbackService,
            backgroundWorkQueue);
    }

    public async Task<ApprovalInstanceResponse> StartAsync(
        TenantId tenantId,
        ApprovalStartRequest request,
        long initiatorUserId,
        CancellationToken cancellationToken)
    {
        // 获取已发布的流程定义
        var flowDef = await _flowRepository.GetByIdAsync(tenantId, request.DefinitionId, cancellationToken);
        if (flowDef == null)
        {
            throw new BusinessException("FLOW_NOT_FOUND", "审批流定义不存在");
        }

        if (flowDef.Status != ApprovalFlowStatus.Published)
        {
            throw new BusinessException("FLOW_NOT_PUBLISHED", "流程定义未发布");
        }

        // 创建实例
        var instance = new ApprovalProcessInstance(
            tenantId,
            request.DefinitionId,
            request.BusinessKey,
            initiatorUserId,
            _idGeneratorAccessor.NextId(),
            request.DataJson);

        // Wrap all persistence operations in a transaction for atomicity
        await ExecuteInTransactionAsync(async () =>
        {
            await _instanceRepository.AddAsync(instance, cancellationToken);

            // 记录实例启动事件
            var startEvent = new ApprovalHistoryEvent(
                tenantId,
                instance.Id,
                ApprovalHistoryEventType.InstanceStarted,
                null,
                null,
                initiatorUserId,
                _idGeneratorAccessor.NextId());
            await _historyRepository.AddAsync(startEvent, cancellationToken);

            // 解析流程定义，生成第一批待审批任务
            var flowDefinition = FlowDefinitionParser.Parse(flowDef.DefinitionJson);
            var startNode = flowDefinition.GetStartNode();
            if (startNode != null)
            {
                // 创建开始节点执行记录
                var startExecution = new ApprovalNodeExecution(
                    tenantId,
                    instance.Id,
                    startNode.Id,
                    ApprovalNodeExecutionStatus.Completed,
                    _idGeneratorAccessor.NextId());
                await _nodeExecutionRepository.AddAsync(startExecution, cancellationToken);

                // 推进到第一个审批节点
                await _flowEngine.AdvanceFlowAsync(tenantId, instance, flowDefinition, startNode.Id, cancellationToken);
                await _instanceRepository.UpdateAsync(instance, cancellationToken);
            }
        }, cancellationToken);

        // Background work (notifications/callbacks) enqueued after transaction commits
        EnqueueNotification(tenantId, ApprovalNotificationEventType.InstanceStarted, instance.Id, null, new[] { initiatorUserId });
        EnqueueCallback(tenantId, CallbackEventType.InstanceStarted, instance.Id, null, null);

        return _mapper.Map<ApprovalInstanceResponse>(instance);
    }

    public async Task ApproveTaskAsync(
        TenantId tenantId,
        long taskId,
        long approverUserId,
        string? comment,
        CancellationToken cancellationToken)
    {
        var task = await _taskRepository.GetByIdAsync(tenantId, taskId, cancellationToken);
        if (task == null)
        {
            throw new BusinessException("TASK_NOT_FOUND", "审批任务不存在");
        }

        if (task.Status != ApprovalTaskStatus.Pending)
        {
            throw new BusinessException("TASK_NOT_PENDING", "任务不是待审批状态");
        }

        // 权限校验：检查审批人是否为任务分配人
        if (task.AssigneeType != AssigneeType.User || task.AssigneeValue != approverUserId.ToString())
        {
            throw new BusinessException("FORBIDDEN", "您无权审批此任务");
        }

        var instance = await _instanceRepository.GetByIdAsync(tenantId, task.InstanceId, cancellationToken);
        if (instance == null || instance.Status != ApprovalInstanceStatus.Running)
        {
            throw new BusinessException("INSTANCE_NOT_RUNNING", "流程实例不在运行状态");
        }

        // Wrap all persistence operations in a transaction for atomicity
        await ExecuteInTransactionAsync(async () =>
        {
            // 记录任务同意事件
            var approveEvent = new ApprovalHistoryEvent(
                tenantId,
                instance.Id,
                ApprovalHistoryEventType.TaskApproved,
                task.NodeId,
                null,
                approverUserId,
                _idGeneratorAccessor.NextId());
            await _historyRepository.AddAsync(approveEvent, cancellationToken);

            // 标记任务为同意
            task.Approve(approverUserId, comment, DateTimeOffset.UtcNow);
            await _taskRepository.UpdateAsync(task, cancellationToken);

            // 推进流程
            var flowDef = await _flowRepository.GetByIdAsync(tenantId, instance.DefinitionId, cancellationToken);
            if (flowDef != null)
            {
                var flowDefinition = FlowDefinitionParser.Parse(flowDef.DefinitionJson);
                await _flowEngine.AdvanceFlowAsync(tenantId, instance, flowDefinition, task.NodeId, cancellationToken);
                await _instanceRepository.UpdateAsync(instance, cancellationToken);
            }
        }, cancellationToken);

        // Background work enqueued after transaction commits
        EnqueueNotification(tenantId, ApprovalNotificationEventType.TaskApproved, instance.Id, task.Id, new[] { instance.InitiatorUserId });
        EnqueueCallback(tenantId, CallbackEventType.TaskApproved, instance.Id, task.Id, task.NodeId);

        // 审批通过且流程已完成时，回写动态表记录状态
        if (instance.Status == ApprovalInstanceStatus.Completed)
        {
            EnqueueStatusSync(tenantId, instance.BusinessKey, "已通过");
        }
    }

    public async Task RejectTaskAsync(
        TenantId tenantId,
        long taskId,
        long approverUserId,
        string? comment,
        CancellationToken cancellationToken)
    {
        var task = await _taskRepository.GetByIdAsync(tenantId, taskId, cancellationToken);
        if (task == null)
        {
            throw new BusinessException("TASK_NOT_FOUND", "审批任务不存在");
        }

        if (task.Status != ApprovalTaskStatus.Pending)
        {
            throw new BusinessException("TASK_NOT_PENDING", "任务不是待审批状态");
        }

        // 权限校验：检查审批人是否为任务分配人
        if (task.AssigneeType != AssigneeType.User || task.AssigneeValue != approverUserId.ToString())
        {
            throw new BusinessException("FORBIDDEN", "您无权审批此任务");
        }

        var instance = await _instanceRepository.GetByIdAsync(tenantId, task.InstanceId, cancellationToken);
        if (instance == null || instance.Status != ApprovalInstanceStatus.Running)
        {
            throw new BusinessException("INSTANCE_NOT_RUNNING", "流程实例不在运行状态");
        }

        // Wrap all persistence operations in a transaction for atomicity
        await ExecuteInTransactionAsync(async () =>
        {
            // 记录任务驳回事件
            var rejectEvent = new ApprovalHistoryEvent(
                tenantId,
                instance.Id,
                ApprovalHistoryEventType.TaskRejected,
                task.NodeId,
                null,
                approverUserId,
                _idGeneratorAccessor.NextId());
            await _historyRepository.AddAsync(rejectEvent, cancellationToken);

            // 标记任务为驳回
            task.Reject(approverUserId, comment, DateTimeOffset.UtcNow);
            await _taskRepository.UpdateAsync(task, cancellationToken);

            // 驳回后，流程实例变为驳回状态，取消所有待审批任务（含 Pending 和 Waiting）
            instance.MarkRejected(DateTimeOffset.UtcNow);
            await _instanceRepository.UpdateAsync(instance, cancellationToken);

            await CancelAllActiveTasksAsync(tenantId, instance.Id, cancellationToken);

            // 记录流程驳回事件
            var instanceRejectEvent = new ApprovalHistoryEvent(
                tenantId,
                instance.Id,
                ApprovalHistoryEventType.InstanceRejected,
                null,
                null,
                approverUserId,
                _idGeneratorAccessor.NextId());
            await _historyRepository.AddAsync(instanceRejectEvent, cancellationToken);
        }, cancellationToken);

        // Background work enqueued after transaction commits
        EnqueueNotification(tenantId, ApprovalNotificationEventType.InstanceRejected, instance.Id, task.Id, new[] { instance.InitiatorUserId });
        EnqueueCallback(tenantId, CallbackEventType.InstanceRejected, instance.Id, task.Id, task.NodeId);
        EnqueueStatusSync(tenantId, instance.BusinessKey, "已驳回");
    }

    public async Task CancelInstanceAsync(
        TenantId tenantId,
        long instanceId,
        long cancelledByUserId,
        CancellationToken cancellationToken)
    {
        var instance = await _instanceRepository.GetByIdAsync(tenantId, instanceId, cancellationToken);
        if (instance == null)
        {
            throw new BusinessException("INSTANCE_NOT_FOUND", "流程实例不存在");
        }

        if (instance.Status != ApprovalInstanceStatus.Running)
        {
            throw new BusinessException("INSTANCE_NOT_RUNNING", "流程实例不在运行状态");
        }

        // Authorization: only the initiator can cancel their own process
        if (instance.InitiatorUserId != cancelledByUserId)
        {
            throw new BusinessException("FORBIDDEN", "只有发起人可以取消流程");
        }

        // Wrap all persistence operations in a transaction for atomicity
        await ExecuteInTransactionAsync(async () =>
        {
            instance.MarkCanceled(DateTimeOffset.UtcNow);
            await _instanceRepository.UpdateAsync(instance, cancellationToken);

            // 取消所有活跃任务（含 Pending 和 Waiting）
            await CancelAllActiveTasksAsync(tenantId, instance.Id, cancellationToken);

            // 记录流程取消事件
            var cancelEvent = new ApprovalHistoryEvent(
                tenantId,
                instance.Id,
                ApprovalHistoryEventType.InstanceCanceled,
                null,
                null,
                cancelledByUserId,
                _idGeneratorAccessor.NextId());
            await _historyRepository.AddAsync(cancelEvent, cancellationToken);
        }, cancellationToken);

        // Background work enqueued after transaction commits
        EnqueueNotification(tenantId, ApprovalNotificationEventType.InstanceCanceled, instance.Id, null, new[] { instance.InitiatorUserId });
        EnqueueCallback(tenantId, CallbackEventType.InstanceCanceled, instance.Id, null, null);
        EnqueueStatusSync(tenantId, instance.BusinessKey, "草稿");
    }

    public async Task MarkCopyRecordAsReadAsync(
        TenantId tenantId,
        long copyRecordId,
        long userId,
        CancellationToken cancellationToken)
    {
        var copyRecord = await _copyRecordRepository.GetByIdAsync(tenantId, copyRecordId, cancellationToken);
        if (copyRecord == null)
        {
            throw new BusinessException("COPY_RECORD_NOT_FOUND", "抄送记录不存在");
        }

        if (copyRecord.RecipientUserId != userId)
        {
            throw new BusinessException("COPY_RECORD_NOT_OWNER", "无权操作此抄送记录");
        }

        if (copyRecord.IsRead)
        {
            // 已读状态，无需重复操作
            return;
        }

        copyRecord.MarkAsRead(DateTimeOffset.UtcNow);
        await _copyRecordRepository.UpdateAsync(copyRecord, cancellationToken);
    }

    #region Transaction & Task Cancellation Helpers

    /// <summary>
    /// Execute an action inside a database transaction if IUnitOfWork is available.
    /// Falls back to direct execution if no UoW is configured.
    /// </summary>
    private async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken)
    {
        if (_unitOfWork != null)
        {
            await _unitOfWork.ExecuteInTransactionAsync(action, cancellationToken);
        }
        else
        {
            await action();
        }
    }

    /// <summary>
    /// Cancel all active tasks (Pending + Waiting) for a given instance.
    /// Bug fix: previously only Pending tasks were cancelled, leaving Waiting tasks (sequential approval) orphaned.
    /// </summary>
    private async Task CancelAllActiveTasksAsync(TenantId tenantId, long instanceId, CancellationToken cancellationToken)
    {
        var pendingTasks = await _taskRepository.GetByInstanceAndStatusAsync(
            tenantId, instanceId, ApprovalTaskStatus.Pending, cancellationToken);
        var waitingTasks = await _taskRepository.GetByInstanceAndStatusAsync(
            tenantId, instanceId, ApprovalTaskStatus.Waiting, cancellationToken);

        var allActiveTasks = new List<ApprovalTask>();
        allActiveTasks.AddRange(pendingTasks);
        allActiveTasks.AddRange(waitingTasks);

        if (allActiveTasks.Count > 0)
        {
            foreach (var task in allActiveTasks)
            {
                task.Cancel();
            }
            await _taskRepository.UpdateRangeAsync(allActiveTasks, cancellationToken);
        }
    }

    #endregion

    #region Background Work Queue Helpers

    /// <summary>
    /// Enqueue a notification to the background work queue.
    /// Each notification executes in its own DI scope, avoiding ObjectDisposedException.
    /// </summary>
    private void EnqueueNotification(
        TenantId tenantId,
        ApprovalNotificationEventType eventType,
        long instanceId,
        long? taskId,
        IReadOnlyList<long> recipientUserIds)
    {
        if (_backgroundWorkQueue == null) return;

        _backgroundWorkQueue.Enqueue(async (sp, ct) =>
        {
            var notificationService = sp.GetService<IApprovalNotificationService>();
            if (notificationService == null) return;

            var instanceRepo = sp.GetRequiredService<IApprovalInstanceRepository>();
            var instance = await instanceRepo.GetByIdAsync(tenantId, instanceId, ct);
            if (instance == null) return;

            ApprovalTask? task = null;
            if (taskId.HasValue)
            {
                var taskRepo = sp.GetRequiredService<IApprovalTaskRepository>();
                task = await taskRepo.GetByIdAsync(tenantId, taskId.Value, ct);
            }

            await notificationService.NotifyAsync(tenantId, eventType, instance, task, recipientUserIds, ct);
        });
    }

    /// <summary>
    /// Enqueue an external callback to the background work queue.
    /// </summary>
    private void EnqueueCallback(
        TenantId tenantId,
        CallbackEventType eventType,
        long instanceId,
        long? taskId,
        string? nodeId)
    {
        if (_backgroundWorkQueue == null) return;

        _backgroundWorkQueue.Enqueue(async (sp, ct) =>
        {
            var callbackService = sp.GetService<ExternalCallbackService>();
            if (callbackService == null) return;

            var instanceRepo = sp.GetRequiredService<IApprovalInstanceRepository>();
            var instance = await instanceRepo.GetByIdAsync(tenantId, instanceId, ct);
            if (instance == null) return;

            ApprovalTask? task = null;
            if (taskId.HasValue)
            {
                var taskRepo = sp.GetRequiredService<IApprovalTaskRepository>();
                task = await taskRepo.GetByIdAsync(tenantId, taskId.Value, ct);
            }

            await callbackService.TriggerCallbackAsync(tenantId, eventType, instance, task, nodeId, ct);
        });
    }

    /// <summary>
    /// Enqueue a status sync (dynamic table writeback) to the background work queue.
    /// </summary>
    private void EnqueueStatusSync(TenantId tenantId, string? businessKey, string status)
    {
        if (_backgroundWorkQueue == null || string.IsNullOrEmpty(businessKey)) return;

        _backgroundWorkQueue.Enqueue(async (sp, ct) =>
        {
            var syncHandler = sp.GetService<ApprovalStatusSyncHandler>();
            if (syncHandler == null) return;

            await syncHandler.SyncStatusAsync(tenantId, businessKey, status, ct);
        });
    }

    #endregion
}

