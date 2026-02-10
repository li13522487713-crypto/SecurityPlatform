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
        IApprovalNotificationService? notificationService = null,
        IApprovalTimeoutReminderRepository? timeoutReminderRepository = null,
        ExternalCallbackService? callbackService = null,
        ApprovalStatusSyncHandler? statusSyncHandler = null,
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
        _notificationService = notificationService;
        _timeoutReminderRepository = timeoutReminderRepository;
        _callbackService = callbackService;
        _statusSyncHandler = statusSyncHandler;
        _idGeneratorAccessor = idGeneratorAccessor;
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
            callbackService);
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

        // 发送流程启动通知（异步，失败不影响主流程）
        if (_notificationService != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _notificationService.NotifyAsync(
                        tenantId,
                        ApprovalNotificationEventType.InstanceStarted,
                        instance,
                        null,
                        new[] { initiatorUserId },
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    // 通知失败不影响主流程，但记录日志
                    _logger?.LogError(ex, "流程启动通知失败：租户={TenantId}, 实例={InstanceId}", tenantId, instance.Id);
                }
            }, cancellationToken);
        }

        // 触发流程启动回调（异步，失败不影响主流程）
        if (_callbackService != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _callbackService.TriggerCallbackAsync(
                        tenantId,
                        Domain.Approval.Enums.CallbackEventType.InstanceStarted,
                        instance,
                        null,
                        null,
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    // 回调失败不影响主流程，但记录日志
                    _logger?.LogError(ex, "流程启动回调失败：租户={TenantId}, 实例={InstanceId}", tenantId, instance.Id);
                }
            }, cancellationToken);
        }

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

        // 发送任务同意通知（异步，失败不影响主流程）
        if (_notificationService != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    // 通知发起人和其他相关人员
                    var recipients = new List<long> { instance.InitiatorUserId };
                    await _notificationService.NotifyAsync(
                        tenantId,
                        ApprovalNotificationEventType.TaskApproved,
                        instance,
                        task,
                        recipients,
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    // 通知失败不影响主流程，但记录日志
                    _logger?.LogError(ex, "任务同意通知失败：租户={TenantId}, 实例={InstanceId}, 任务={TaskId}", tenantId, instance.Id, task.Id);
                }
            }, cancellationToken);
        }

        // 触发任务同意回调（异步，失败不影响主流程）
        if (_callbackService != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _callbackService.TriggerCallbackAsync(
                        tenantId,
                        Domain.Approval.Enums.CallbackEventType.TaskApproved,
                        instance,
                        task,
                        task.NodeId,
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    // 回调失败不影响主流程，但记录日志
                    _logger?.LogError(ex, "任务同意回调失败：租户={TenantId}, 实例={InstanceId}, 任务={TaskId}", tenantId, instance.Id, task.Id);
                }
            }, cancellationToken);
        }

        // 推进流程
        var flowDef = await _flowRepository.GetByIdAsync(tenantId, instance.DefinitionId, cancellationToken);
        if (flowDef != null)
        {
            var flowDefinition = FlowDefinitionParser.Parse(flowDef.DefinitionJson);
            await _flowEngine.AdvanceFlowAsync(tenantId, instance, flowDefinition, task.NodeId, cancellationToken);
            await _instanceRepository.UpdateAsync(instance, cancellationToken);

            // 审批通过且流程已完成时，回写动态表记录状态
            if (instance.Status == ApprovalInstanceStatus.Completed && _statusSyncHandler != null)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _statusSyncHandler.SyncStatusAsync(tenantId, instance.BusinessKey, "已通过", CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "审批通过状态回写失败：租户={TenantId}, 实例={InstanceId}", tenantId, instance.Id);
                    }
                }, cancellationToken);
            }
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

        // 驳回后，流程实例变为驳回状态，取消所有待审批任务
        instance.MarkRejected(DateTimeOffset.UtcNow);
        await _instanceRepository.UpdateAsync(instance, cancellationToken);

        var pendingTasks = await _taskRepository.GetByInstanceAndStatusAsync(
            tenantId,
            instance.Id,
            ApprovalTaskStatus.Pending,
            cancellationToken);
        
        // 批量更新任务状态
        if (pendingTasks.Count > 0)
        {
            foreach (var pendingTask in pendingTasks)
            {
                pendingTask.Cancel();
            }
            await _taskRepository.UpdateRangeAsync(pendingTasks, cancellationToken);
        }

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

        // 发送流程驳回通知（异步，失败不影响主流程）
        if (_notificationService != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    // 通知发起人
                    await _notificationService.NotifyAsync(
                        tenantId,
                        ApprovalNotificationEventType.InstanceRejected,
                        instance,
                        task,
                        new[] { instance.InitiatorUserId },
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    // 通知失败不影响主流程，但记录日志
                    _logger?.LogError(ex, "流程驳回通知失败：租户={TenantId}, 实例={InstanceId}", tenantId, instance.Id);
                }
            }, cancellationToken);
        }

        // 触发流程驳回回调（异步，失败不影响主流程）
        if (_callbackService != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _callbackService.TriggerCallbackAsync(
                        tenantId,
                        Domain.Approval.Enums.CallbackEventType.InstanceRejected,
                        instance,
                        task,
                        task.NodeId,
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    // 回调失败不影响主流程，但记录日志
                    _logger?.LogError(ex, "流程驳回回调失败：租户={TenantId}, 实例={InstanceId}", tenantId, instance.Id);
                }
            }, cancellationToken);
        }

        // 驳回时，回写动态表记录状态为"已驳回"
        if (_statusSyncHandler != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _statusSyncHandler.SyncStatusAsync(tenantId, instance.BusinessKey, "已驳回", CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "审批驳回状态回写失败：租户={TenantId}, 实例={InstanceId}", tenantId, instance.Id);
                }
            }, cancellationToken);
        }
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

        instance.MarkCanceled(DateTimeOffset.UtcNow);
        await _instanceRepository.UpdateAsync(instance, cancellationToken);

        // 取消所有待审批任务
        var pendingTasks = await _taskRepository.GetByInstanceAndStatusAsync(
            tenantId,
            instance.Id,
            ApprovalTaskStatus.Pending,
            cancellationToken);
        
        // 批量更新任务状态
        if (pendingTasks.Count > 0)
        {
            foreach (var task in pendingTasks)
            {
                task.Cancel();
            }
            await _taskRepository.UpdateRangeAsync(pendingTasks, cancellationToken);
        }

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

        // 发送流程取消通知（异步，失败不影响主流程）
        if (_notificationService != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    // 通知发起人和所有待办任务的处理人
                    var recipients = new List<long> { instance.InitiatorUserId };
                    foreach (var pendingTask in pendingTasks)
                    {
                        // 从任务中提取处理人（需要解析 AssigneeValue）
                        // TODO: 简化处理，暂时只通知发起人
                    }
                    await _notificationService.NotifyAsync(
                        tenantId,
                        ApprovalNotificationEventType.InstanceCanceled,
                        instance,
                        null,
                        recipients,
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    // 通知失败不影响主流程，但记录日志
                    _logger?.LogError(ex, "流程取消通知失败：租户={TenantId}, 实例={InstanceId}", tenantId, instance.Id);
                }
            }, cancellationToken);
        }

        // 触发流程取消回调（异步，失败不影响主流程）
        if (_callbackService != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _callbackService.TriggerCallbackAsync(
                        tenantId,
                        Domain.Approval.Enums.CallbackEventType.InstanceCanceled,
                        instance,
                        null,
                        null,
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    // 回调失败不影响主流程，但记录日志
                    _logger?.LogError(ex, "流程取消回调失败：租户={TenantId}, 实例={InstanceId}", tenantId, instance.Id);
                }
            }, cancellationToken);
        }

        // 取消时，回写动态表记录状态为"草稿"（允许重新提交）
        if (_statusSyncHandler != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _statusSyncHandler.SyncStatusAsync(tenantId, instance.BusinessKey, "草稿", CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "审批取消状态回写失败：租户={TenantId}, 实例={InstanceId}", tenantId, instance.Id);
                }
            }, cancellationToken);
        }
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
}

