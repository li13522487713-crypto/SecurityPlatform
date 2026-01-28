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
    private readonly IIdGenerator _idGenerator;
    private readonly IMapper _mapper;
    private readonly FlowEngine _flowEngine;

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
        IIdGenerator idGenerator,
        IMapper mapper)
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
        _idGenerator = idGenerator;
        _mapper = mapper;
        var conditionEvaluator = new ConditionEvaluator(processVariableRepository);
        _flowEngine = new FlowEngine(taskRepository, nodeExecutionRepository, deptLeaderRepository, parallelTokenRepository, copyRecordRepository, conditionEvaluator, userQueryService, idGenerator);
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
            _idGenerator.NextId(),
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
            _idGenerator.NextId());
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
                _idGenerator.NextId());
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
            _idGenerator.NextId());
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
            _idGenerator.NextId());
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
        foreach (var pendingTask in pendingTasks)
        {
            pendingTask.Cancel();
            await _taskRepository.UpdateAsync(pendingTask, cancellationToken);
        }

        // 记录流程驳回事件
        var instanceRejectEvent = new ApprovalHistoryEvent(
            tenantId,
            instance.Id,
            ApprovalHistoryEventType.InstanceRejected,
            null,
            null,
            approverUserId,
            _idGenerator.NextId());
        await _historyRepository.AddAsync(instanceRejectEvent, cancellationToken);
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
        foreach (var task in pendingTasks)
        {
            task.Cancel();
            await _taskRepository.UpdateAsync(task, cancellationToken);
        }

        // 记录流程取消事件
        var cancelEvent = new ApprovalHistoryEvent(
            tenantId,
            instance.Id,
            ApprovalHistoryEventType.InstanceCanceled,
            null,
            null,
            cancelledByUserId,
            _idGenerator.NextId());
        await _historyRepository.AddAsync(cancelEvent, cancellationToken);
    }
}
