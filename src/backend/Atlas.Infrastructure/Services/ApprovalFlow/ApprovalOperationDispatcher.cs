using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;
using System.Text.Json;

namespace Atlas.Infrastructure.Services.ApprovalFlow;

/// <summary>
/// 审批流操作分发器（根据操作类型分发到对应的处理器，支持幂等性检查）
/// </summary>
public sealed class ApprovalOperationDispatcher
{
    private readonly Dictionary<ApprovalOperationType, IApprovalOperationHandler> _handlers;
    private readonly IApprovalOperationRecordRepository _operationRecordRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public ApprovalOperationDispatcher(
        IEnumerable<IApprovalOperationHandler> handlers,
        IApprovalOperationRecordRepository operationRecordRepository,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _handlers = handlers.ToDictionary(h => h.SupportedOperationType);
        _operationRecordRepository = operationRecordRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
    }

    /// <summary>
    /// 分发操作到对应的处理器（支持幂等性检查）
    /// </summary>
    public async Task DispatchAsync(
        TenantId tenantId,
        long instanceId,
        long? taskId,
        long operatorUserId,
        ApprovalOperationRequest request,
        CancellationToken cancellationToken)
    {
        // 幂等性检查：如果提供了幂等键，检查是否已执行过
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existingRecord = await _operationRecordRepository.FindByIdempotencyKeyAsync(
                tenantId, instanceId, request.IdempotencyKey, cancellationToken);

            if (existingRecord != null)
            {
                if (existingRecord.Status == ApprovalOperationStatus.Completed)
                {
                    // 操作已完成，直接返回（幂等）
                    return;
                }

                if (existingRecord.Status == ApprovalOperationStatus.Pending)
                {
                    // 操作正在处理中，抛出异常
                    throw new BusinessException("OPERATION_IN_PROGRESS", "操作正在处理中，请勿重复提交");
                }

                // 操作失败，允许重试（但需要新的幂等键）
                throw new BusinessException("OPERATION_FAILED", "上次操作失败，请使用新的幂等键重试");
            }

            // 创建操作记录（Pending 状态）
            var operationRecord = new ApprovalOperationRecord(
                tenantId,
                instanceId,
                request.OperationType,
                request.IdempotencyKey,
                operatorUserId,
                _idGeneratorAccessor.NextId(),
                taskId,
                JsonSerializer.Serialize(request));

            await _operationRecordRepository.AddAsync(operationRecord, cancellationToken);

            try
            {
                // 执行操作
                if (!_handlers.TryGetValue(request.OperationType, out var handler))
                {
                    throw new InvalidOperationException($"不支持的操作类型: {request.OperationType}");
                }

                await handler.ExecuteAsync(tenantId, instanceId, taskId, operatorUserId, request, cancellationToken);

                // 标记操作完成
                operationRecord.MarkCompleted(DateTimeOffset.UtcNow);
                await _operationRecordRepository.UpdateAsync(operationRecord, cancellationToken);
            }
            catch (Exception ex)
            {
                // 标记操作失败
                operationRecord.MarkFailed(ex.Message, DateTimeOffset.UtcNow);
                await _operationRecordRepository.UpdateAsync(operationRecord, cancellationToken);
                throw;
            }
        }
        else
        {
            // 未提供幂等键，生成一个并记录操作（确保审计完整性）
            var generatedIdempotencyKey = $"auto-{request.OperationType}-{instanceId}-{taskId}-{operatorUserId}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
            
            var operationRecord = new ApprovalOperationRecord(
                tenantId,
                instanceId,
                request.OperationType,
                generatedIdempotencyKey,
                operatorUserId,
                _idGeneratorAccessor.NextId(),
                taskId,
                JsonSerializer.Serialize(request));

            await _operationRecordRepository.AddAsync(operationRecord, cancellationToken);

            try
            {
                if (!_handlers.TryGetValue(request.OperationType, out var handler))
                {
                    throw new InvalidOperationException($"不支持的操作类型: {request.OperationType}");
                }

                await handler.ExecuteAsync(tenantId, instanceId, taskId, operatorUserId, request, cancellationToken);

                // 标记操作完成
                operationRecord.MarkCompleted(DateTimeOffset.UtcNow);
                await _operationRecordRepository.UpdateAsync(operationRecord, cancellationToken);
            }
            catch (Exception ex)
            {
                // 标记操作失败
                operationRecord.MarkFailed(ex.Message, DateTimeOffset.UtcNow);
                await _operationRecordRepository.UpdateAsync(operationRecord, cancellationToken);
                throw;
            }
        }
    }
}




