using Atlas.Application.Approval.Abstractions;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.ApprovalFlow;

/// <summary>
/// Handles approval domain events to sync status back to dynamic table records.
/// This is the decoupled replacement for direct ApprovalStatusSyncHandler calls.
/// The BusinessKey format "{tableKey}:{recordId}" is parsed here, keeping this
/// format knowledge in the dynamic table module, not the approval module.
/// </summary>
public sealed class DynamicTableApprovalEventHandler : IApprovalEventHandler
{
    private readonly ApprovalStatusSyncHandler _syncHandler;
    private readonly ILogger<DynamicTableApprovalEventHandler>? _logger;

    public DynamicTableApprovalEventHandler(
        ApprovalStatusSyncHandler syncHandler,
        ILogger<DynamicTableApprovalEventHandler>? logger = null)
    {
        _syncHandler = syncHandler;
        _logger = logger;
    }

    public async Task OnInstanceCompletedAsync(ApprovalInstanceEvent e, CancellationToken ct)
    {
        await _syncHandler.SyncStatusAsync(e.TenantId, e.BusinessKey, "已通过", ct);
    }

    public async Task OnInstanceRejectedAsync(ApprovalInstanceEvent e, CancellationToken ct)
    {
        await _syncHandler.SyncStatusAsync(e.TenantId, e.BusinessKey, "已驳回", ct);
    }

    public async Task OnInstanceCanceledAsync(ApprovalInstanceEvent e, CancellationToken ct)
    {
        await _syncHandler.SyncStatusAsync(e.TenantId, e.BusinessKey, "草稿", ct);
    }
}
