using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IWorkflowTraceService
{
    Task<WorkflowTraceSnapshotDto?> GetTraceAsync(
        TenantId tenantId,
        string executionId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkflowTraceSpanDto>> ListSpansAsync(
        TenantId tenantId,
        string workflowId,
        CancellationToken cancellationToken);
}

public interface IWorkflowCollaboratorService
{
    Task<IReadOnlyList<WorkflowCollaboratorDto>> ListAsync(
        TenantId tenantId,
        string workflowId,
        CancellationToken cancellationToken);

    Task OpenAsync(
        TenantId tenantId,
        string workflowId,
        CancellationToken cancellationToken);

    Task CloseAsync(
        TenantId tenantId,
        string workflowId,
        CancellationToken cancellationToken);
}

public interface IWorkflowTriggerService
{
    Task<string> SaveAsync(
        TenantId tenantId,
        string workflowId,
        string name,
        string eventType,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkflowTriggerDto>> ListTriggersAsync(
        TenantId tenantId,
        string workflowId,
        CancellationToken cancellationToken);

    Task<bool> TestRunAsync(
        TenantId tenantId,
        string workflowId,
        string triggerId,
        CancellationToken cancellationToken);
}

public interface IWorkflowJobService
{
    Task<string> CreateAsync(
        TenantId tenantId,
        string workflowId,
        string name,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkflowJobDto>> ListJobsAsync(
        TenantId tenantId,
        string workflowId,
        CancellationToken cancellationToken);

    Task CancelAsync(
        TenantId tenantId,
        string jobId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkflowTaskDto>> ListTasksAsync(
        TenantId tenantId,
        string jobId,
        CancellationToken cancellationToken);
}

public interface IChatFlowRoleService
{
    Task<string> SaveAsync(
        TenantId tenantId,
        string workflowId,
        string name,
        string description,
        string? avatarUri,
        CancellationToken cancellationToken);

    Task<ChatFlowRoleDto?> GetAsync(
        TenantId tenantId,
        string roleId,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        TenantId tenantId,
        string roleId,
        CancellationToken cancellationToken);
}
