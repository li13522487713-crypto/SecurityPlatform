using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Models;
using Atlas.Application.Approval.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;
using Atlas.Infrastructure.Services.ApprovalFlow;
using Models = Atlas.Application.Approval.Models;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 审批流运行时操作服务实现
/// </summary>
public sealed class ApprovalOperationService : IApprovalOperationService
{
    private readonly ApprovalOperationDispatcher _dispatcher;
    private readonly IApprovalOperationRecordRepository _operationRecordRepository;
    private readonly IIdGenerator _idGenerator;

    public ApprovalOperationService(
        ApprovalOperationDispatcher dispatcher,
        IApprovalOperationRecordRepository operationRecordRepository,
        IIdGenerator idGenerator)
    {
        _dispatcher = dispatcher;
        _operationRecordRepository = operationRecordRepository;
        _idGenerator = idGenerator;
    }

    public async Task ExecuteOperationAsync(
        TenantId tenantId,
        long instanceId,
        long? taskId,
        long operatorUserId,
        Models.ApprovalOperationRequest request,
        CancellationToken cancellationToken)
    {
        var operationRequest = new Atlas.Application.Approval.Abstractions.ApprovalOperationRequest
        {
            OperationType = request.OperationType,
            Comment = request.Comment,
            TargetNodeId = request.TargetNodeId,
            TargetAssigneeValue = request.TargetAssigneeValue,
            AdditionalAssigneeValues = request.AdditionalAssigneeValues,
            IdempotencyKey = request.IdempotencyKey
        };

        await _dispatcher.DispatchAsync(tenantId, instanceId, taskId, operatorUserId, operationRequest, cancellationToken);
    }

    public async Task RecordUiOperationAsync(
        TenantId tenantId,
        long instanceId,
        long? taskId,
        long operatorUserId,
        ApprovalOperationType operationType,
        CancellationToken cancellationToken)
    {
        // 创建操作记录（UI操作不需要幂等性检查，直接记录）
        var operationRecord = new ApprovalOperationRecord(
            tenantId,
            instanceId,
            operationType,
            $"ui-{operationType}-{instanceId}-{operatorUserId}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
            operatorUserId,
            _idGenerator.NextId(),
            taskId,
            null);

        operationRecord.MarkCompleted(DateTimeOffset.UtcNow);
        await _operationRecordRepository.AddAsync(operationRecord, cancellationToken);
    }
}
