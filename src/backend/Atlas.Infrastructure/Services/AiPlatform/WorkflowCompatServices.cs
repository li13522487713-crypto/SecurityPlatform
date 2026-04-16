using System.Collections.Concurrent;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Infrastructure.Services.AiPlatform;

/// <summary>
/// Coze 兼容层扩展服务的轻量实现，先提供可用接口契约，后续可逐步替换为持久化实现。
/// </summary>
public sealed class WorkflowCompatServices :
    IWorkflowTraceService,
    IWorkflowCollaboratorService,
    IWorkflowTriggerService,
    IWorkflowJobService,
    IChatFlowRoleService
{
    private static readonly ConcurrentDictionary<string, ChatFlowRoleDto> RoleStore = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, List<WorkflowTriggerDto>> TriggerStore = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, List<WorkflowJobDto>> JobStore = new(StringComparer.OrdinalIgnoreCase);

    private readonly IIdGeneratorAccessor _idGenerator;

    public WorkflowCompatServices(IIdGeneratorAccessor idGenerator)
    {
        _idGenerator = idGenerator;
    }

    public Task<WorkflowTraceSnapshotDto?> GetTraceAsync(
        TenantId tenantId,
        string executionId,
        CancellationToken cancellationToken)
    {
        var snapshot = new WorkflowTraceSnapshotDto(
            executionId,
            ExecutionStatus.Completed,
            Array.Empty<WorkflowTraceSpanDto>());
        return Task.FromResult<WorkflowTraceSnapshotDto?>(snapshot);
    }

    public Task<IReadOnlyList<WorkflowTraceSpanDto>> ListSpansAsync(
        TenantId tenantId,
        string workflowId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<WorkflowTraceSpanDto>>(Array.Empty<WorkflowTraceSpanDto>());
    }

    public Task<IReadOnlyList<WorkflowCollaboratorDto>> ListAsync(
        TenantId tenantId,
        string workflowId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<WorkflowCollaboratorDto>>(Array.Empty<WorkflowCollaboratorDto>());
    }

    public Task OpenAsync(TenantId tenantId, string workflowId, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task CloseAsync(TenantId tenantId, string workflowId, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task<string> SaveAsync(
        TenantId tenantId,
        string workflowId,
        string name,
        string eventType,
        CancellationToken cancellationToken)
    {
        var triggerId = _idGenerator.NextId().ToString();
        var trigger = new WorkflowTriggerDto(triggerId, workflowId, name, eventType, true);
        TriggerStore.AddOrUpdate(
            workflowId,
            _ => new List<WorkflowTriggerDto> { trigger },
            (_, list) =>
            {
                list.Add(trigger);
                return list;
            });
        return Task.FromResult(triggerId);
    }

    public Task<IReadOnlyList<WorkflowTriggerDto>> ListTriggersAsync(
        TenantId tenantId,
        string workflowId,
        CancellationToken cancellationToken)
    {
        if (TriggerStore.TryGetValue(workflowId, out var list))
        {
            return Task.FromResult<IReadOnlyList<WorkflowTriggerDto>>(list);
        }

        return Task.FromResult<IReadOnlyList<WorkflowTriggerDto>>(Array.Empty<WorkflowTriggerDto>());
    }

    public Task<bool> TestRunAsync(
        TenantId tenantId,
        string workflowId,
        string triggerId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }

    public Task<string> CreateAsync(
        TenantId tenantId,
        string workflowId,
        string name,
        CancellationToken cancellationToken)
    {
        var jobId = _idGenerator.NextId().ToString();
        var dto = new WorkflowJobDto(jobId, workflowId, name, "pending", DateTime.UtcNow);
        JobStore.AddOrUpdate(
            workflowId,
            _ => new List<WorkflowJobDto> { dto },
            (_, list) =>
            {
                list.Add(dto);
                return list;
            });
        return Task.FromResult(jobId);
    }

    public Task<IReadOnlyList<WorkflowJobDto>> ListJobsAsync(
        TenantId tenantId,
        string workflowId,
        CancellationToken cancellationToken)
    {
        if (JobStore.TryGetValue(workflowId, out var list))
        {
            return Task.FromResult<IReadOnlyList<WorkflowJobDto>>(list);
        }

        return Task.FromResult<IReadOnlyList<WorkflowJobDto>>(Array.Empty<WorkflowJobDto>());
    }

    public Task CancelAsync(TenantId tenantId, string jobId, CancellationToken cancellationToken)
    {
        foreach (var entry in JobStore)
        {
            for (var i = 0; i < entry.Value.Count; i++)
            {
                if (!string.Equals(entry.Value[i].Id, jobId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                entry.Value[i] = entry.Value[i] with { Status = "cancelled" };
                return Task.CompletedTask;
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<WorkflowTaskDto>> ListTasksAsync(
        TenantId tenantId,
        string jobId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<WorkflowTaskDto>>(Array.Empty<WorkflowTaskDto>());
    }

    public Task<string> SaveAsync(
        TenantId tenantId,
        string workflowId,
        string name,
        string description,
        string? avatarUri,
        CancellationToken cancellationToken)
    {
        var roleId = _idGenerator.NextId().ToString();
        var dto = new ChatFlowRoleDto(roleId, workflowId, name, description, avatarUri);
        RoleStore[roleId] = dto;
        return Task.FromResult(roleId);
    }

    public Task<ChatFlowRoleDto?> GetAsync(
        TenantId tenantId,
        string roleId,
        CancellationToken cancellationToken)
    {
        RoleStore.TryGetValue(roleId, out var dto);
        return Task.FromResult(dto);
    }

    public Task DeleteAsync(
        TenantId tenantId,
        string roleId,
        CancellationToken cancellationToken)
    {
        RoleStore.TryRemove(roleId, out _);
        return Task.CompletedTask;
    }
}
