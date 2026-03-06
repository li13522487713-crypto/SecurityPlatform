using AutoMapper;
using System.Text.Json;
using System.Diagnostics;
using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Models;
using Atlas.Application.Approval.Repositories;
using Atlas.Application.Identity.Abstractions;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;
using Atlas.Infrastructure.Services.ApprovalFlow;
using Atlas.Infrastructure.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ApprovalNodeExecutionStatus = Atlas.Domain.Approval.Entities.ApprovalNodeExecutionStatus;

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
    private readonly IApprovalNodeExecutionRepository _nodeExecutionRepository;
    private readonly IApprovalParallelTokenRepository _parallelTokenRepository;
    private readonly IApprovalCopyRecordRepository _copyRecordRepository;
    private readonly IApprovalSubProcessLinkRepository _subProcessLinkRepository;
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
    private readonly IRbacResolver _rbacResolver;

    public ApprovalRuntimeCommandService(
        IApprovalFlowRepository flowRepository,
        IApprovalInstanceRepository instanceRepository,
        IApprovalTaskRepository taskRepository,
        IApprovalHistoryRepository historyRepository,
        IApprovalNodeExecutionRepository nodeExecutionRepository,
        IApprovalParallelTokenRepository parallelTokenRepository,
        IApprovalCopyRecordRepository copyRecordRepository,
        IApprovalSubProcessLinkRepository subProcessLinkRepository,
        FlowEngine flowEngine,
        IIdGeneratorAccessor idGeneratorAccessor,
        IMapper mapper,
        IUnitOfWork? unitOfWork = null,
        IApprovalNotificationService? notificationService = null,
        IApprovalTimeoutReminderRepository? timeoutReminderRepository = null,
        ExternalCallbackService? callbackService = null,
        ApprovalStatusSyncHandler? statusSyncHandler = null,
        IBackgroundWorkQueue? backgroundWorkQueue = null,
        ILogger<ApprovalRuntimeCommandService>? logger = null,
        IRbacResolver? rbacResolver = null)
    {
        _flowRepository = flowRepository;
        _instanceRepository = instanceRepository;
        _taskRepository = taskRepository;
        _historyRepository = historyRepository;
        _nodeExecutionRepository = nodeExecutionRepository;
        _parallelTokenRepository = parallelTokenRepository;
        _copyRecordRepository = copyRecordRepository;
        _subProcessLinkRepository = subProcessLinkRepository;
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _timeoutReminderRepository = timeoutReminderRepository;
        _callbackService = callbackService;
        _statusSyncHandler = statusSyncHandler;
        _idGeneratorAccessor = idGeneratorAccessor;
        _backgroundWorkQueue = backgroundWorkQueue;
        _mapper = mapper;
        _logger = logger;
        _flowEngine = flowEngine;
        _rbacResolver = rbacResolver ?? throw new ArgumentNullException(nameof(rbacResolver));
    }

    public async Task<ApprovalInstanceResponse> StartAsync(
        TenantId tenantId,
        ApprovalStartRequest request,
        long initiatorUserId,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var status = "success";
        try
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

            if (flowDef.IsDeprecated)
            {
                throw new BusinessException("FLOW_DEPRECATED", "流程定义已弃用，不允许新发起实例");
            }

            // 幂等性检查：同一 BusinessKey 已存在运行中实例，直接返回
            if (!string.IsNullOrEmpty(request.BusinessKey))
            {
                var existingInstance = await _instanceRepository.GetByBusinessKeyAsync(tenantId, request.BusinessKey, cancellationToken);
                if (existingInstance != null && existingInstance.Status == ApprovalInstanceStatus.Running)
                {
                    _logger?.LogInformation(
                        "审批发起幂等命中：BusinessKey={BusinessKey}, InstanceId={InstanceId}",
                        request.BusinessKey, existingInstance.Id);
                    return _mapper.Map<ApprovalInstanceResponse>(existingInstance);
                }
            }

            // 创建实例
            var instance = new ApprovalProcessInstance(
                tenantId,
                request.DefinitionId,
                request.BusinessKey,
                initiatorUserId,
                _idGeneratorAccessor.NextId(),
                request.DataJson);
            
            // 穿越时空：覆盖创建时间
            if (request.OverrideCreateTime.HasValue)
            {
                instance.OverrideStartedAt(request.OverrideCreateTime.Value);
            }

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
        catch
        {
            status = "failed";
            throw;
        }
        finally
        {
            AtlasMetrics.RecordApprovalStart(stopwatch.Elapsed.TotalMilliseconds, status);
        }
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
        CancellationToken cancellationToken,
        string? targetNodeId = null)
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

            // 取消当前节点其他活跃任务（会签/或签中的其他任务）
            await CancelAllActiveTasksAsync(tenantId, instance.Id, cancellationToken);

            // 解析流程定义，尝试驳回路由
            var flowDef = await _flowRepository.GetByIdAsync(tenantId, instance.DefinitionId, cancellationToken);
            var routed = false;
            if (flowDef != null)
            {
                var flowDefinition = FlowDefinitionParser.Parse(flowDef.DefinitionJson);

                var executionContext = new FlowExecutionContext();
                routed = await _flowEngine.HandleRejectionAsync(
                    tenantId, instance, flowDefinition, task.NodeId, targetNodeId, cancellationToken, executionContext);

                if (routed)
                {
                    // 驳回已路由到目标节点，流程继续运行
                    await _instanceRepository.UpdateAsync(instance, cancellationToken);
                }
            }

            if (!routed)
            {
                // 无法路由或策略为终止，终止流程
                instance.MarkRejected(DateTimeOffset.UtcNow);
                await _instanceRepository.UpdateAsync(instance, cancellationToken);

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
            }
        }, cancellationToken);

        // Background work enqueued after transaction commits
        if (instance.Status == ApprovalInstanceStatus.Rejected)
        {
            EnqueueNotification(tenantId, ApprovalNotificationEventType.InstanceRejected, instance.Id, task.Id, new[] { instance.InitiatorUserId });
            EnqueueCallback(tenantId, CallbackEventType.InstanceRejected, instance.Id, task.Id, task.NodeId);
            EnqueueStatusSync(tenantId, instance.BusinessKey, "已驳回");
        }
        else
        {
            // 驳回已路由，通知相关人员
            EnqueueNotification(tenantId, ApprovalNotificationEventType.TaskRejected, instance.Id, task.Id, new[] { instance.InitiatorUserId });
            EnqueueCallback(tenantId, CallbackEventType.TaskRejected, instance.Id, task.Id, task.NodeId);
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

    public async Task DelegateTaskAsync(
        TenantId tenantId,
        long taskId,
        long delegatorUserId,
        long delegateeUserId,
        string? comment,
        CancellationToken cancellationToken)
    {
        var task = await _taskRepository.GetByIdAsync(tenantId, taskId, cancellationToken);
        if (task == null) throw new BusinessException("TASK_NOT_FOUND", "任务不存在");
        if (task.Status != ApprovalTaskStatus.Pending) throw new BusinessException("TASK_NOT_PENDING", "任务状态不正确");

        // 标记原任务为已委派
        task.Delegate(delegatorUserId, delegateeUserId.ToString());
        await _taskRepository.UpdateAsync(task, cancellationToken);

        // 创建新任务给被委派人
        var delegateTask = new ApprovalTask(
            tenantId,
            task.InstanceId,
            task.NodeId,
            $"[委派] {task.Title}",
            AssigneeType.User,
            delegateeUserId.ToString(),
            _idGeneratorAccessor.NextId(),
            order: task.Order,
            initialStatus: ApprovalTaskStatus.Pending);
        
        delegateTask.SetParentTaskId(task.Id);
        delegateTask.SetTaskType(11); // 委派任务

        await _taskRepository.AddAsync(delegateTask, cancellationToken);

        // 记录委派历史
        var historyEvent = new ApprovalHistoryEvent(
            tenantId,
            task.InstanceId,
            ApprovalHistoryEventType.TaskTransferred, // 使用 TaskTransferred 或新增 Delegated
            task.NodeId,
            null,
            delegatorUserId,
            _idGeneratorAccessor.NextId());
        await _historyRepository.AddAsync(historyEvent, cancellationToken);
    }

    public async Task ResolveTaskAsync(
        TenantId tenantId,
        long taskId,
        long resolverUserId,
        string? comment,
        CancellationToken cancellationToken)
    {
        var task = await _taskRepository.GetByIdAsync(tenantId, taskId, cancellationToken);
        if (task == null) throw new BusinessException("TASK_NOT_FOUND", "任务不存在");
        
        // 只有委派任务可以被"解决"（归还）
        if (task.TaskType != 11) throw new BusinessException("INVALID_OPERATION", "非委派任务不能归还");

        // 标记委派任务完成
        task.Approve(resolverUserId, comment, DateTimeOffset.UtcNow);
        await _taskRepository.UpdateAsync(task, cancellationToken);

        // 找到原任务并激活
        if (task.ParentTaskId.HasValue)
        {
            var parentTask = await _taskRepository.GetByIdAsync(tenantId, task.ParentTaskId.Value, cancellationToken);
            if (parentTask != null && parentTask.Status == ApprovalTaskStatus.Delegated)
            {
                parentTask.ClaimBack(); // 变回 Pending
                await _taskRepository.UpdateAsync(parentTask, cancellationToken);
            }
        }
    }

    public async Task StartSubProcessAsync(
        TenantId tenantId,
        long parentInstanceId,
        string parentNodeId,
        long childProcessId,
        bool isAsync,
        CancellationToken cancellationToken)
    {
        // 1. 获取子流程定义
        var flowDef = await _flowRepository.GetByIdAsync(tenantId, childProcessId, cancellationToken);
        if (flowDef == null) throw new BusinessException("FLOW_NOT_FOUND", "子流程定义不存在");

        // 2. 获取父流程实例信息作为子流程输入
        var parentInstance = await _instanceRepository.GetByIdAsync(tenantId, parentInstanceId, cancellationToken);
        if (parentInstance == null) throw new BusinessException("INSTANCE_NOT_FOUND", "父流程实例不存在");

        // 3. 创建子流程实例
        var childInstance = new ApprovalProcessInstance(
            tenantId,
            childProcessId,
            parentInstance.BusinessKey, // 共享业务Key
            parentInstance.InitiatorUserId, // 继承发起人
            _idGeneratorAccessor.NextId(),
            parentInstance.DataJson);
        
        childInstance.SetParentInstanceId(parentInstanceId);
        
        await _instanceRepository.AddAsync(childInstance, cancellationToken);

        // 4. 记录关联关系
        var link = new ApprovalSubProcessLink(
            tenantId,
            parentInstanceId,
            parentNodeId,
            childInstance.Id,
            childProcessId,
            isAsync,
            _idGeneratorAccessor.NextId());

        await _subProcessLinkRepository.AddAsync(link, cancellationToken);

        // 5. 记录启动事件
        var startEvent = new ApprovalHistoryEvent(
            tenantId,
            childInstance.Id,
            ApprovalHistoryEventType.InstanceStarted,
            null,
            null,
            parentInstance.InitiatorUserId,
            _idGeneratorAccessor.NextId());
        await _historyRepository.AddAsync(startEvent, cancellationToken);

        // 6. 启动子流程
        var flowDefinition = FlowDefinitionParser.Parse(flowDef.DefinitionJson);
        var startNode = flowDefinition.GetStartNode();
        if (startNode != null)
        {
            // 创建开始节点执行记录
            var startExecution = new ApprovalNodeExecution(
                tenantId,
                childInstance.Id,
                startNode.Id,
                ApprovalNodeExecutionStatus.Completed,
                _idGeneratorAccessor.NextId());
            await _nodeExecutionRepository.AddAsync(startExecution, cancellationToken);

            await _flowEngine.AdvanceFlowAsync(tenantId, childInstance, flowDefinition, startNode.Id, cancellationToken);
            await _instanceRepository.UpdateAsync(childInstance, cancellationToken);
        }
    }

    public async Task SuspendInstanceAsync(
        TenantId tenantId,
        long instanceId,
        long operatorUserId,
        CancellationToken cancellationToken)
    {
        var instance = await _instanceRepository.GetByIdAsync(tenantId, instanceId, cancellationToken);
        if (instance == null) throw new BusinessException("INSTANCE_NOT_FOUND", "实例不存在");

        await EnsureInstanceOperationPermissionAsync(tenantId, instance, operatorUserId, cancellationToken);
        instance.Suspend();
        await _instanceRepository.UpdateAsync(instance, cancellationToken);
    }

    public async Task ActivateInstanceAsync(
        TenantId tenantId,
        long instanceId,
        long operatorUserId,
        CancellationToken cancellationToken)
    {
        var instance = await _instanceRepository.GetByIdAsync(tenantId, instanceId, cancellationToken);
        if (instance == null) throw new BusinessException("INSTANCE_NOT_FOUND", "实例不存在");

        await EnsureInstanceOperationPermissionAsync(tenantId, instance, operatorUserId, cancellationToken);
        instance.Activate();
        await _instanceRepository.UpdateAsync(instance, cancellationToken);
    }

    public async Task TerminateInstanceAsync(
        TenantId tenantId,
        long instanceId,
        long operatorUserId,
        string? comment,
        CancellationToken cancellationToken)
    {
        var instance = await _instanceRepository.GetByIdAsync(tenantId, instanceId, cancellationToken);
        if (instance == null) throw new BusinessException("INSTANCE_NOT_FOUND", "实例不存在");

        await EnsureInstanceOperationPermissionAsync(tenantId, instance, operatorUserId, cancellationToken);
        instance.Terminate(DateTimeOffset.UtcNow);
        await _instanceRepository.UpdateAsync(instance, cancellationToken);
        
        // 取消所有任务
        await CancelAllActiveTasksAsync(tenantId, instanceId, cancellationToken);
    }

    public async Task<ApprovalInstanceResponse> SaveDraftAsync(
        TenantId tenantId,
        ApprovalStartRequest request,
        long initiatorUserId,
        CancellationToken cancellationToken)
    {
        // 创建实例但不启动
        var instance = new ApprovalProcessInstance(
            tenantId,
            request.DefinitionId,
            request.BusinessKey,
            initiatorUserId,
            _idGeneratorAccessor.NextId(),
            request.DataJson);
        
        instance.SaveAsDraft();
        await _instanceRepository.AddAsync(instance, cancellationToken);
        
        return _mapper.Map<ApprovalInstanceResponse>(instance);
    }

    public async Task<ApprovalInstanceResponse> SubmitDraftAsync(
        TenantId tenantId,
        long instanceId,
        long initiatorUserId,
        CancellationToken cancellationToken)
    {
        var instance = await _instanceRepository.GetByIdAsync(tenantId, instanceId, cancellationToken);
        if (instance == null) throw new BusinessException("INSTANCE_NOT_FOUND", "草稿不存在");
        if (instance.Status != ApprovalInstanceStatus.Draft) throw new BusinessException("INVALID_STATUS", "非草稿状态");
        await EnsureDraftSubmitPermissionAsync(tenantId, instance, initiatorUserId, cancellationToken);

        instance.Activate(); // 变更为 Running
        await _instanceRepository.UpdateAsync(instance, cancellationToken);

        // 启动流程逻辑（复用 StartAsync 的部分逻辑）
        var flowDef = await _flowRepository.GetByIdAsync(tenantId, instance.DefinitionId, cancellationToken);
        if (flowDef != null)
        {
            var flowDefinition = FlowDefinitionParser.Parse(flowDef.DefinitionJson);
            var startNode = flowDefinition.GetStartNode();
            if (startNode != null)
            {
                await _flowEngine.AdvanceFlowAsync(tenantId, instance, flowDefinition, startNode.Id, cancellationToken);
                await _instanceRepository.UpdateAsync(instance, cancellationToken);
            }
        }

        return _mapper.Map<ApprovalInstanceResponse>(instance);
    }

    public async Task<int> BatchTransferTasksAsync(
        TenantId tenantId,
        long fromUserId,
        long toUserId,
        long operatorUserId,
        CancellationToken cancellationToken)
    {
        var tasks = await _taskRepository.GetPendingByAssigneeUserAsync(tenantId, fromUserId, cancellationToken);
        if (tasks.Count == 0) return 0;

        await ExecuteInTransactionAsync(async () =>
        {
            var historyEvents = new List<ApprovalHistoryEvent>(tasks.Count);
            foreach (var task in tasks)
            {
                task.Transfer(toUserId.ToString());
                historyEvents.Add(new ApprovalHistoryEvent(
                    tenantId,
                    task.InstanceId,
                    ApprovalHistoryEventType.TaskTransferred,
                    task.NodeId,
                    $"批量转办 {fromUserId} -> {toUserId}",
                    operatorUserId,
                    _idGeneratorAccessor.NextId()));
            }

            await _taskRepository.UpdateRangeAsync(tasks, cancellationToken);
            await _historyRepository.AddRangeAsync(historyEvents, cancellationToken);
        }, cancellationToken);

        return tasks.Count;
    }

    #region Transaction & Task Cancellation Helpers    /// <summary>
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

    private async Task EnsureInstanceOperationPermissionAsync(
        TenantId tenantId,
        ApprovalProcessInstance instance,
        long operatorUserId,
        CancellationToken cancellationToken)
    {
        if (instance.InitiatorUserId == operatorUserId)
        {
            return;
        }

        var roleCodes = await _rbacResolver.GetRoleCodesAsync(tenantId, operatorUserId, cancellationToken);
        var isAdmin = roleCodes.Contains("Admin", StringComparer.OrdinalIgnoreCase)
            || roleCodes.Contains("SuperAdmin", StringComparer.OrdinalIgnoreCase);
        if (isAdmin)
        {
            return;
        }

        var hasTaskAccess = await _taskRepository.ExistsByInstanceAndAssigneeAsync(
            tenantId,
            instance.Id,
            operatorUserId,
            cancellationToken);
        if (hasTaskAccess)
        {
            return;
        }

        throw new BusinessException("FORBIDDEN", "您无权操作该流程实例");
    }

    private async Task EnsureDraftSubmitPermissionAsync(
        TenantId tenantId,
        ApprovalProcessInstance draft,
        long operatorUserId,
        CancellationToken cancellationToken)
    {
        if (draft.InitiatorUserId == operatorUserId)
        {
            return;
        }

        var roleCodes = await _rbacResolver.GetRoleCodesAsync(tenantId, operatorUserId, cancellationToken);
        var isAdmin = roleCodes.Contains("Admin", StringComparer.OrdinalIgnoreCase)
            || roleCodes.Contains("SuperAdmin", StringComparer.OrdinalIgnoreCase);
        if (isAdmin)
        {
            return;
        }

        throw new BusinessException("FORBIDDEN", "只有发起人或管理员可以提交草稿");
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

