using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Models;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Services.ApprovalFlow;
using Models = Atlas.Application.Approval.Models;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 审批流运行时操作服务实现
/// </summary>
public sealed class ApprovalOperationService : IApprovalOperationService
{
    private readonly ApprovalOperationDispatcher _dispatcher;

    public ApprovalOperationService(ApprovalOperationDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
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
            AdditionalAssigneeValues = request.AdditionalAssigneeValues
        };

        await _dispatcher.DispatchAsync(tenantId, instanceId, taskId, operatorUserId, operationRequest, cancellationToken);
    }
}
