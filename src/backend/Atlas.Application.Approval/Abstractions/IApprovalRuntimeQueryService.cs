using Atlas.Application.Approval.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Application.Approval.Abstractions;

/// <summary>
/// 审批流运行时查询服务接口
/// </summary>
public interface IApprovalRuntimeQueryService
{
    Task<ApprovalInstanceResponse?> GetInstanceByIdAsync(
        TenantId tenantId,
        long instanceId,
        CancellationToken cancellationToken);

    Task<PagedResult<ApprovalInstanceListItem>> GetInstancesByInitiatorAsync(
        TenantId tenantId,
        long initiatorUserId,
        PagedRequest request,
        ApprovalInstanceStatus? status = null,
        CancellationToken cancellationToken = default);

    Task<PagedResult<ApprovalInstanceListItem>> GetInstancesPagedAsync(
        TenantId tenantId,
        PagedRequest request,
        long? definitionId = null,
        long? initiatorUserId = null,
        DateTimeOffset? startedFrom = null,
        DateTimeOffset? startedTo = null,
        string? businessKey = null,
        ApprovalInstanceStatus? status = null,
        CancellationToken cancellationToken = default);

    Task<PagedResult<ApprovalTaskResponse>> GetTasksByInstanceAsync(
        TenantId tenantId,
        long instanceId,
        PagedRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<ApprovalTaskResponse>> GetMyTasksAsync(
        TenantId tenantId,
        long userId,
        PagedRequest request,
        ApprovalTaskStatus? status = null,
        long? flowDefinitionId = null,
        CancellationToken cancellationToken = default);

    Task<PagedResult<ApprovalHistoryEventResponse>> GetHistoryAsync(
        TenantId tenantId,
        long instanceId,
        PagedRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<ApprovalCopyRecordResponse>> GetMyCopyRecordsAsync(
        TenantId tenantId,
        long userId,
        PagedRequest request,
        bool? isRead = null,
        CancellationToken cancellationToken = default);

    Task<bool> HasInstanceAccessAsync(
        TenantId tenantId,
        long instanceId,
        long userId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 审批历史事件响应
/// </summary>
public record ApprovalHistoryEventResponse
{
    public required long Id { get; init; }
    public required string EventType { get; init; }
    public string? FromNode { get; init; }
    public string? ToNode { get; init; }
    public string? PayloadJson { get; init; }
    public long? ActorUserId { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
}
