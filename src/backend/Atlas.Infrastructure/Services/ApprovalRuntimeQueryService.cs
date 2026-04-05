using AutoMapper;
using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Models;
using Atlas.Application.Approval.Repositories;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Repositories;
using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Repositories;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
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
    private readonly IApprovalTimeoutReminderRepository _timeoutReminderRepository;
    private readonly ITenantDataScopeFilter _dataScopeFilter;
    private readonly IUserDepartmentRepository _userDepartmentRepository;
    private readonly IProjectUserRepository _projectUserRepository;
    private readonly IAppContextAccessor _appContextAccessor;
    private readonly IAppDataScopeFilter _appDataScopeFilter;
    private readonly IAppMemberDepartmentRepository _appMemberDepartmentRepository;
    private readonly IMapper _mapper;

    public ApprovalRuntimeQueryService(
        IApprovalInstanceRepository instanceRepository,
        IApprovalFlowRepository flowRepository,
        IApprovalTaskRepository taskRepository,
        IApprovalHistoryRepository historyRepository,
        IApprovalCopyRecordRepository copyRecordRepository,
        IApprovalTimeoutReminderRepository timeoutReminderRepository,
        ITenantDataScopeFilter dataScopeFilter,
        IUserDepartmentRepository userDepartmentRepository,
        IProjectUserRepository projectUserRepository,
        IAppContextAccessor appContextAccessor,
        IAppDataScopeFilter appDataScopeFilter,
        IAppMemberDepartmentRepository appMemberDepartmentRepository,
        IMapper mapper)
    {
        _instanceRepository = instanceRepository;
        _flowRepository = flowRepository;
        _taskRepository = taskRepository;
        _historyRepository = historyRepository;
        _copyRecordRepository = copyRecordRepository;
        _timeoutReminderRepository = timeoutReminderRepository;
        _dataScopeFilter = dataScopeFilter;
        _userDepartmentRepository = userDepartmentRepository;
        _projectUserRepository = projectUserRepository;
        _appContextAccessor = appContextAccessor;
        _appDataScopeFilter = appDataScopeFilter;
        _appMemberDepartmentRepository = appMemberDepartmentRepository;
        _mapper = mapper;
    }

    public async Task<ApprovalInstanceResponse?> GetInstanceByIdAsync(
        TenantId tenantId,
        long instanceId,
        CancellationToken cancellationToken)
    {
        var entity = await _instanceRepository.GetByIdAsync(tenantId, instanceId, cancellationToken);
        if (entity == null)
        {
            return null;
        }

        var initiatorRestriction = await ResolveInitiatorUserRestrictionAsync(tenantId, cancellationToken);
        if (initiatorRestriction is not null && !initiatorRestriction.Contains(entity.InitiatorUserId))
        {
            return null;
        }

        var flow = await _flowRepository.GetByIdAsync(tenantId, entity.DefinitionId, cancellationToken);
        var reminders = await _timeoutReminderRepository.GetByInstanceAsync(tenantId, entity.Id, cancellationToken);
        var (expectedCompleteTime, remainingMinutes) = GetSla(reminders.Where(x => !x.IsCompleted));
        var mapped = _mapper.Map<ApprovalInstanceResponse>(entity);
        return mapped with
        {
            FlowName = flow?.Name,
            CurrentNodeName = entity.CurrentNodeName,
            ExpectedCompleteTime = expectedCompleteTime,
            SlaRemainingMinutes = remainingMinutes
        };
    }

    public async Task<PagedResult<ApprovalInstanceListItem>> GetInstancesByInitiatorAsync(
        TenantId tenantId,
        long initiatorUserId,
        PagedRequest request,
        ApprovalInstanceStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var initiatorRestriction = await ResolveInitiatorUserRestrictionAsync(tenantId, cancellationToken);
        if (initiatorRestriction is not null && !initiatorRestriction.Contains(initiatorUserId))
        {
            return new PagedResult<ApprovalInstanceListItem>(
                Array.Empty<ApprovalInstanceListItem>(),
                0,
                request.PageIndex,
                request.PageSize);
        }

        var (items, totalCount) = await _instanceRepository.GetPagedByInitiatorAsync(
            tenantId,
            initiatorUserId,
            request.PageIndex,
            request.PageSize,
            status,
            restrictInitiatorUserIds: null,
            cancellationToken);

        var listItems = await BuildInstanceListItemsAsync(tenantId, items, cancellationToken);

        return new PagedResult<ApprovalInstanceListItem>(
            listItems,
            totalCount,
            request.PageIndex,
            request.PageSize);
    }

    public async Task<PagedResult<ApprovalInstanceListItem>> GetInstancesPagedAsync(
        TenantId tenantId,
        PagedRequest request,
        long? definitionId = null,
        long? initiatorUserId = null,
        DateTimeOffset? startedFrom = null,
        DateTimeOffset? startedTo = null,
        string? businessKey = null,
        ApprovalInstanceStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var initiatorRestriction = await ResolveInitiatorUserRestrictionAsync(tenantId, cancellationToken);
        if (initiatorUserId.HasValue && initiatorRestriction is not null
            && !initiatorRestriction.Contains(initiatorUserId.Value))
        {
            return new PagedResult<ApprovalInstanceListItem>(
                Array.Empty<ApprovalInstanceListItem>(),
                0,
                request.PageIndex,
                request.PageSize);
        }

        var (items, totalCount) = await _instanceRepository.GetPagedAsync(
            tenantId,
            request.PageIndex,
            request.PageSize,
            definitionId,
            initiatorUserId,
            startedFrom,
            startedTo,
            businessKey,
            status,
            initiatorRestriction,
            cancellationToken);

        var listItems = await BuildInstanceListItemsAsync(tenantId, items, cancellationToken);

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

        var enrichedItems = await BuildTaskResponsesAsync(tenantId, items, cancellationToken);

        return new PagedResult<ApprovalTaskResponse>(
            enrichedItems,
            totalCount,
            request.PageIndex,
            request.PageSize);
    }

    public async Task<PagedResult<ApprovalTaskResponse>> GetMyTasksAsync(
        TenantId tenantId,
        long userId,
        PagedRequest request,
        ApprovalTaskStatus? status = null,
        long? flowDefinitionId = null,
        CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _taskRepository.GetPagedByAssigneeAsync(
            tenantId,
            userId,
            request.PageIndex,
            request.PageSize,
            status,
            flowDefinitionId,
            cancellationToken);

        var enrichedItems = await BuildTaskResponsesAsync(tenantId, items, cancellationToken);

        return new PagedResult<ApprovalTaskResponse>(
            enrichedItems,
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

    private async Task<List<ApprovalTaskResponse>> BuildTaskResponsesAsync(
        TenantId tenantId,
        IReadOnlyList<ApprovalTask> tasks,
        CancellationToken cancellationToken)
    {
        if (tasks.Count == 0)
        {
            return new List<ApprovalTaskResponse>();
        }

        var instanceIds = tasks.Select(x => x.InstanceId).Distinct().ToArray();
        var instances = await _instanceRepository.QueryByIdsAsync(tenantId, instanceIds, cancellationToken);
        var instanceMap = instances.ToDictionary(x => x.Id);
        var definitionIds = instances.Select(x => x.DefinitionId).Distinct().ToArray();
        var flows = await _flowRepository.QueryByIdsAsync(tenantId, definitionIds, cancellationToken);
        var flowMap = flows.ToDictionary(x => x.Id, x => x.Name);
        var reminders = await _timeoutReminderRepository.GetByInstancesAsync(tenantId, instanceIds, cancellationToken);

        return tasks.Select(task =>
        {
            instanceMap.TryGetValue(task.InstanceId, out var instance);
            string? flowName = null;
            if (instance != null && flowMap.TryGetValue(instance.DefinitionId, out var name))
            {
                flowName = name;
            }

            var taskReminders = reminders.Where(reminder =>
                reminder.InstanceId == task.InstanceId
                && !reminder.IsCompleted
                && (reminder.TaskId == task.Id || (reminder.TaskId == null && reminder.NodeId == task.NodeId)));
            var (expectedCompleteTime, remainingMinutes) = GetSla(taskReminders);

            var mapped = _mapper.Map<ApprovalTaskResponse>(task);
            return mapped with
            {
                FlowName = flowName,
                CurrentNodeName = instance?.CurrentNodeName,
                ExpectedCompleteTime = expectedCompleteTime,
                SlaRemainingMinutes = remainingMinutes
            };
        }).ToList();
    }

    private async Task<List<ApprovalInstanceListItem>> BuildInstanceListItemsAsync(
        TenantId tenantId,
        IReadOnlyList<ApprovalProcessInstance> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return new List<ApprovalInstanceListItem>();
        }

        var definitionIds = items.Select(x => x.DefinitionId).Distinct().ToArray();
        var flows = await _flowRepository.QueryByIdsAsync(tenantId, definitionIds, cancellationToken);
        var flowMap = flows.ToDictionary(x => x.Id, x => x.Name);
        var reminders = await _timeoutReminderRepository.GetByInstancesAsync(tenantId, items.Select(x => x.Id).ToArray(), cancellationToken);

        return items.Select(item =>
        {
            var flowName = flowMap.TryGetValue(item.DefinitionId, out var name) ? name : "Unknown";
            var (_, remainingMinutes) = GetSla(reminders.Where(x => x.InstanceId == item.Id && !x.IsCompleted));
            return new ApprovalInstanceListItem
            {
                Id = item.Id,
                DefinitionId = item.DefinitionId,
                FlowName = flowName,
                BusinessKey = item.BusinessKey,
                InitiatorUserId = item.InitiatorUserId,
                Status = item.Status,
                StartedAt = item.StartedAt,
                EndedAt = item.EndedAt,
                CurrentNodeName = item.CurrentNodeName,
                SlaRemainingMinutes = remainingMinutes,
                DataJson = item.DataJson
            };
        }).ToList();
    }

    private static (DateTimeOffset? ExpectedCompleteTime, int? RemainingMinutes) GetSla(IEnumerable<ApprovalTimeoutReminder> reminders)
    {
        var nearest = reminders.OrderBy(x => x.ExpectedCompleteTime).FirstOrDefault();
        if (nearest == null)
        {
            return (null, null);
        }

        var remainingMinutes = (int)Math.Floor((nearest.ExpectedCompleteTime - DateTimeOffset.UtcNow).TotalMinutes);
        return (nearest.ExpectedCompleteTime, remainingMinutes);
    }

    /// <summary>
    /// 按数据权限解析允许查看的审批发起人用户 ID；null 表示不限制。
    /// </summary>
    private async Task<long[]?> ResolveInitiatorUserRestrictionAsync(
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var resolvedAppId = _appContextAccessor.ResolveAppId();
        if (resolvedAppId is > 0)
        {
            var appOwnerId = await _appDataScopeFilter.GetOwnerFilterIdAsync(resolvedAppId.Value, cancellationToken);
            if (appOwnerId.HasValue)
            {
                return new[] { appOwnerId.Value };
            }

            var appDeptIds = await _appDataScopeFilter.GetDeptFilterIdsAsync(resolvedAppId.Value, cancellationToken);
            if (appDeptIds is not null)
            {
                if (appDeptIds.Count == 0)
                {
                    return Array.Empty<long>();
                }

                var appDeptUserIds = await _appMemberDepartmentRepository.QueryUserIdsByDepartmentIdsAsync(
                    tenantId,
                    resolvedAppId.Value,
                    appDeptIds,
                    cancellationToken);
                return appDeptUserIds.Distinct().ToArray();
            }

            return null;
        }

        var ownerId = await _dataScopeFilter.GetOwnerFilterIdAsync(cancellationToken);
        if (ownerId.HasValue)
        {
            return new[] { ownerId.Value };
        }

        var deptIds = await _dataScopeFilter.GetDeptFilterIdsAsync(cancellationToken);
        if (deptIds is not null)
        {
            if (deptIds.Count == 0)
            {
                return Array.Empty<long>();
            }

            var userIds = await _userDepartmentRepository.QueryUserIdsByDepartmentIdsAsync(
                tenantId,
                deptIds,
                cancellationToken);
            return userIds.Distinct().ToArray();
        }

        var projectIds = await _dataScopeFilter.GetProjectFilterIdsAsync(cancellationToken);
        if (projectIds is not null)
        {
            if (projectIds.Count == 0)
            {
                return Array.Empty<long>();
            }

            var userIds = await _projectUserRepository.QueryUserIdsByProjectIdsAsync(
                tenantId,
                projectIds,
                cancellationToken);
            return userIds.Distinct().ToArray();
        }

        return null;
    }
}
