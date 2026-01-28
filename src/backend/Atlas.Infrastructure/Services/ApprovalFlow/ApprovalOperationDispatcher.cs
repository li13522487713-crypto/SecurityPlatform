using Atlas.Application.Approval.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Infrastructure.Services.ApprovalFlow;

/// <summary>
/// 审批流操作分发器（根据操作类型分发到对应的处理器）
/// </summary>
public sealed class ApprovalOperationDispatcher
{
    private readonly Dictionary<ApprovalOperationType, IApprovalOperationHandler> _handlers;

    public ApprovalOperationDispatcher(IEnumerable<IApprovalOperationHandler> handlers)
    {
        _handlers = handlers.ToDictionary(h => h.SupportedOperationType);
    }

    /// <summary>
    /// 分发操作到对应的处理器
    /// </summary>
    public async Task DispatchAsync(
        TenantId tenantId,
        long instanceId,
        long? taskId,
        long operatorUserId,
        ApprovalOperationRequest request,
        CancellationToken cancellationToken)
    {
        if (!_handlers.TryGetValue(request.OperationType, out var handler))
        {
            throw new InvalidOperationException($"不支持的操作类型: {request.OperationType}");
        }

        await handler.ExecuteAsync(tenantId, instanceId, taskId, operatorUserId, request, cancellationToken);
    }
}
