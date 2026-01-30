using AutoMapper;
using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Models;
using Atlas.Application.Approval.Repositories;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 审批流运行时查询服务实现
/// </summary>
public sealed class ApprovalRuntimeQueryService : IApprovalRuntimeQueryService
{
    private readonly IApprovalInstanceRepository _instanceRepository;
    private readonly IApprovalFlowRepository _flowRepository;
    private readonly IApprovalTaskRepository _taskRepository;
    private readonly IApprovalHistoryRepository _historyRepository;
    private readonly IApprovalCopyRecordRepository _copyRecordRepository;
    private readonly IMapper _mapper;

    public ApprovalRuntimeQueryService(
        IApprovalInstanceRepository instanceRepository,
        IApprovalFlowRepository flowRepository,
        IApprovalTaskRepository taskRepository,
        IApprovalHistoryRepository historyRepository,
        IApprovalCopyRecordRepository copyRecordRepository,
        IMapper mapper)
    {
        _instanceRepository = instanceRepository;
        _flowRepository = flowRepository;
        _taskRepository = taskRepository;
        _historyRepository = historyRepository;
        _copyRecordRepository = copyRecordRepository;
        _mapper = mapper;
    }

    public async Task<ApprovalInstanceResponse?> GetInstanceByIdAsync(
        TenantId tenantId,
        long instanceId,
        CancellationToken cancellationToken)
    {
        var entity = await _instanceRepository.GetByIdAsync(tenantId, instanceId, cancellationToken);
        return entity != null ? _mapper.Map<ApprovalInstanceResponse>(entity) : null;
    }

    public async Task<PagedResult<ApprovalInstanceListItem>> GetInstancesByInitiatorAsync(
        TenantId tenantId,
        long initiatorUserId,
        PagedRequest request,
        ApprovalInstanceStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _instanceRepository.GetPagedByInitiatorAsync(
            tenantId,
            initiatorUserId,
            request.PageIndex,
            request.PageSize,
            status,
            cancellationToken);

        var listItems = new List<ApprovalInstanceListItem>();
        foreach (var item in items)
        {
            var flow = await _flowRepository.GetByIdAsync(tenantId, item.DefinitionId, cancellationToken);
            listItems.Add(new ApprovalInstanceListItem
            {
                Id = item.Id,
                DefinitionId = item.DefinitionId,
                FlowName = flow?.Name ?? "Unknown",
                BusinessKey = item.BusinessKey,
                InitiatorUserId = item.InitiatorUserId,
                Status = item.Status,
                StartedAt = item.StartedAt,
                EndedAt = item.EndedAt
            });
        }

        return new PagedResult<ApprovalInstanceListItem>(
            listItems,
            totalCount,
            request.PageIndex,
            request.PageSize);
    }

    public async Task<PagedResult<ApprovalTaskResponse>> GetTasksByInstanceAsync(
        TenantId tenantId,
        long instanceId,
        PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _taskRepository.GetPagedByInstanceAsync(
            tenantId,
            instanceId,
            request.PageIndex,
            request.PageSize,
            cancellationToken: cancellationToken);

        return new PagedResult<ApprovalTaskResponse>(
            _mapper.Map<List<ApprovalTaskResponse>>(items),
            totalCount,
            request.PageIndex,
            request.PageSize);
    }

    public async Task<PagedResult<ApprovalTaskResponse>> GetMyTasksAsync(
        TenantId tenantId,
        long userId,
        PagedRequest request,
        ApprovalTaskStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _taskRepository.GetPagedByAssigneeAsync(
            tenantId,
            userId,
            request.PageIndex,
            request.PageSize,
            status,
            cancellationToken);

        return new PagedResult<ApprovalTaskResponse>(
            _mapper.Map<List<ApprovalTaskResponse>>(items),
            totalCount,
            request.PageIndex,
            request.PageSize);
    }

    public async Task<PagedResult<ApprovalHistoryEventResponse>> GetHistoryAsync(
        TenantId tenantId,
        long instanceId,
        PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _historyRepository.GetPagedByInstanceAsync(
            tenantId,
            instanceId,
            request.PageIndex,
            request.PageSize,
            cancellationToken);

        var historyItems = items.Select(x => new ApprovalHistoryEventResponse
        {
            Id = x.Id,
            EventType = x.EventType.ToString(),
            FromNode = x.FromNode,
            ToNode = x.ToNode,
            PayloadJson = x.PayloadJson,
            ActorUserId = x.ActorUserId,
            OccurredAt = x.OccurredAt
        }).ToList();

        return new PagedResult<ApprovalHistoryEventResponse>(
            historyItems,
            totalCount,
            request.PageIndex,
            request.PageSize);
    }

    public async Task<PagedResult<ApprovalCopyRecordResponse>> GetMyCopyRecordsAsync(
        TenantId tenantId,
        long userId,
        PagedRequest request,
        bool? isRead = null,
        CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _copyRecordRepository.GetPagedByRecipientAsync(
            tenantId,
            userId,
            request.PageIndex,
            request.PageSize,
            isRead,
            cancellationToken);

        var responseItems = items.Select(x => new ApprovalCopyRecordResponse
        {
            Id = x.Id,
            InstanceId = x.InstanceId,
            NodeId = x.NodeId,
            RecipientUserId = x.RecipientUserId,
            IsRead = x.IsRead,
            CreatedAt = x.CreatedAt,
            ReadAt = x.ReadAt
        }).ToList();

        return new PagedResult<ApprovalCopyRecordResponse>(
            responseItems,
            totalCount,
            request.PageIndex,
            request.PageSize);
    }

    public async Task<bool> HasInstanceAccessAsync(
        TenantId tenantId,
        long instanceId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        var hasTask = await _taskRepository.ExistsByInstanceAndAssigneeAsync(
            tenantId,
            instanceId,
            userId,
            cancellationToken);
        if (hasTask)
        {
            return true;
        }

        return await _copyRecordRepository.ExistsByInstanceAndRecipientAsync(
            tenantId,
            instanceId,
            userId,
            cancellationToken);
    }
}
