using Atlas.Application.LowCode.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LowCode.Abstractions;

public interface IWorkflowGenerationService
{
    Task<WorkflowGenerationResult> GenerateAsync(TenantId tenantId, long currentUserId, WorkflowGenerationRequest request, CancellationToken cancellationToken);
}

public interface IWorkflowBatchService
{
    Task<BatchExecuteResult> ExecuteBatchAsync(TenantId tenantId, long currentUserId, BatchExecuteRequest request, CancellationToken cancellationToken);
}

public interface IWorkflowCompositionService
{
    Task<WorkflowComposeResult> ComposeAsync(TenantId tenantId, long currentUserId, WorkflowComposeRequest request, CancellationToken cancellationToken);
    Task DecomposeAsync(TenantId tenantId, long currentUserId, WorkflowDecomposeRequest request, CancellationToken cancellationToken);
}

public interface IWorkflowQuotaService
{
    Task<WorkflowQuotaDto> GetQuotaAsync(TenantId tenantId, CancellationToken cancellationToken);
    /// <summary>检查配额；超出抛 BusinessException("WORKFLOW_QUOTA_EXCEEDED", ...)。</summary>
    Task EnsureWithinQuotaAsync(TenantId tenantId, CancellationToken cancellationToken);
}
