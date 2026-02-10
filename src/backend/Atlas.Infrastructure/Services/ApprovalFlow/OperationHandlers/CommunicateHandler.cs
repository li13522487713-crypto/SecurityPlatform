using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;
using SqlSugar;

namespace Atlas.Infrastructure.Services.ApprovalFlow.OperationHandlers;

/// <summary>
/// 沟通处理器
/// </summary>
public sealed class CommunicateHandler : IApprovalOperationHandler
{
    private readonly ISqlSugarClient _db;
    private readonly IApprovalHistoryRepository _historyRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public CommunicateHandler(
        ISqlSugarClient db,
        IApprovalHistoryRepository historyRepository,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _db = db;
        _historyRepository = historyRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
    }

    public ApprovalOperationType SupportedOperationType => ApprovalOperationType.Communicate;

    public async Task ExecuteAsync(
        TenantId tenantId,
        long instanceId,
        long? taskId,
        long operatorUserId,
        ApprovalOperationRequest request,
        CancellationToken cancellationToken)
    {
        if (!taskId.HasValue) throw new BusinessException("INVALID_REQUEST", "任务ID不能为空");
        if (string.IsNullOrEmpty(request.TargetAssigneeValue)) throw new BusinessException("INVALID_REQUEST", "沟通对象不能为空");

        if (!long.TryParse(request.TargetAssigneeValue, out var recipientId))
        {
             throw new BusinessException("INVALID_REQUEST", "无效的沟通对象ID");
        }

        // 创建沟通记录
        var record = new ApprovalCommunicationRecord(
            tenantId,
            instanceId,
            taskId.Value,
            operatorUserId,
            recipientId,
            request.Comment ?? string.Empty,
            _idGeneratorAccessor.NextId());
        
        await _db.Insertable(record).ExecuteCommandAsync(cancellationToken);

        // 记录历史
        var historyEvent = new ApprovalHistoryEvent(
            tenantId,
            instanceId,
            ApprovalHistoryEventType.TaskCommunicated,
            null,
            request.Comment,
            operatorUserId,
            _idGeneratorAccessor.NextId());
        await _historyRepository.AddAsync(historyEvent, cancellationToken);
    }
}
