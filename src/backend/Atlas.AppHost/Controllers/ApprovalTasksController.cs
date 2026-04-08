using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AutoMapper;
using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Models;
using Atlas.Application.Approval.Repositories;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Audit.Models;
using Atlas.Core.Exceptions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;
using FluentValidation;
using Atlas.Presentation.Shared.Authorization;
using Atlas.Presentation.Shared.Helpers;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.AppHost.Controllers;

/// <summary>
/// 审批任务控制器（我的待办、审批/驳回等）
/// </summary>
[ApiController]
[Route("api/v1/approval/tasks")]
[Authorize]
public sealed class ApprovalTasksController : ControllerBase
{
    private readonly IApprovalRuntimeQueryService _queryService;
    private readonly IApprovalRuntimeCommandService _commandService;
    private readonly IApprovalTaskRepository _taskRepository;
    private readonly IApprovalCommunicationRecordRepository _communicationRecordRepository;
    private readonly IApprovalOperationService _operationService;
    private readonly IMapper _mapper;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IClientContextAccessor _clientContextAccessor;
    private readonly IValidator<ApprovalTaskDecideRequest> _decideValidator;
    private readonly IAuditRecorder _auditRecorder;

    public ApprovalTasksController(
        IApprovalRuntimeQueryService queryService,
        IApprovalRuntimeCommandService commandService,
        IApprovalTaskRepository taskRepository,
        IApprovalCommunicationRecordRepository communicationRecordRepository,
        IApprovalOperationService operationService,
        IMapper mapper,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IClientContextAccessor clientContextAccessor,
        IValidator<ApprovalTaskDecideRequest> decideValidator,
        IAuditRecorder auditRecorder)
    {
        _queryService = queryService;
        _commandService = commandService;
        _taskRepository = taskRepository;
        _communicationRecordRepository = communicationRecordRepository;
        _operationService = operationService;
        _mapper = mapper;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _clientContextAccessor = clientContextAccessor;
        _decideValidator = decideValidator;
        _auditRecorder = auditRecorder;
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowView)]
    public async Task<ApiResponse<ApprovalTaskResponse>> GetTaskDetailAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var task = await _taskRepository.GetByIdAsync(tenantId, id, cancellationToken);
        if (task is null)
        {
            return ApiResponse<ApprovalTaskResponse>.Fail("NOT_FOUND", ApiResponseLocalizer.T(HttpContext, "ApprovalTaskNotFound"), HttpContext.TraceIdentifier);
        }

        var response = _mapper.Map<ApprovalTaskResponse>(task);
        return ApiResponse<ApprovalTaskResponse>.Ok(response, HttpContext.TraceIdentifier);
    }

    [HttpGet("my")]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowView)]
    public async Task<ApiResponse<PagedResult<ApprovalTaskResponse>>> GetMyTasksAsync(
        [FromQuery] PagedRequest request,
        [FromQuery] ApprovalTaskStatus? status = null,
        [FromQuery] long? flowDefinitionId = null,
        CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var result = await _queryService.GetMyTasksAsync(
            currentUser.TenantId,
            currentUser.UserId,
            request,
            status,
            flowDefinitionId,
            cancellationToken);
        return ApiResponse<PagedResult<ApprovalTaskResponse>>.Ok(result, HttpContext.TraceIdentifier);
    }

    [HttpGet("~/api/v1/approval/instances/{instanceId:long}/tasks")]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowView)]
    public async Task<ApiResponse<PagedResult<ApprovalTaskResponse>>> GetByInstanceAsync(
        long instanceId,
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetTasksByInstanceAsync(tenantId, instanceId, request, cancellationToken);
        return ApiResponse<PagedResult<ApprovalTaskResponse>>.Ok(result, HttpContext.TraceIdentifier);
    }

    [HttpPost("{id:long}/delegation")]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowUpdate)]
    public async Task<ApiResponse<string>> DelegateAsync(
        long id,
        [FromQuery] long delegateeUserId,
        [FromBody] string? comment,
        CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();

        await _commandService.DelegateTaskAsync(
            currentUser.TenantId,
            id,
            currentUser.UserId,
            delegateeUserId,
            comment,
            cancellationToken);

        return ApiResponse<string>.Ok(ApiResponseLocalizer.T(HttpContext, "ApprovalTaskDelegatedSuccess"), HttpContext.TraceIdentifier);
    }

    [HttpPost("{id:long}/resolution")]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowUpdate)]
    public async Task<ApiResponse<string>> ResolveAsync(
        long id,
        [FromBody] string? comment,
        CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();

        await _commandService.ResolveTaskAsync(
            currentUser.TenantId,
            id,
            currentUser.UserId,
            comment,
            cancellationToken);

        return ApiResponse<string>.Ok(ApiResponseLocalizer.T(HttpContext, "ApprovalTaskReturnedSuccess"), HttpContext.TraceIdentifier);
    }

    [HttpPost("{id:long}/claim")]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowUpdate)]
    public async Task<ApiResponse<string>> ClaimAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();

        var task = await _taskRepository.GetByIdAsync(currentUser.TenantId, id, cancellationToken);
        if (task is null)
        {
            return ApiResponse<string>.Fail("NOT_FOUND", ApiResponseLocalizer.T(HttpContext, "ApprovalTaskNotFound"), HttpContext.TraceIdentifier);
        }

        var request = new Atlas.Application.Approval.Models.ApprovalOperationRequest
        {
            OperationType = ApprovalOperationType.Claim
        };

        await _operationService.ExecuteOperationAsync(
            currentUser.TenantId,
            task.InstanceId,
            id,
            currentUser.UserId,
            request,
            cancellationToken);

        return ApiResponse<string>.Ok(ApiResponseLocalizer.T(HttpContext, "ApprovalTaskClaimedSuccess"), HttpContext.TraceIdentifier);
    }

    [HttpPost("{id:long}/urge")]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowUpdate)]
    public async Task<ApiResponse<string>> UrgeAsync(
        long id,
        [FromBody] string? message,
        CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();

        var task = await _taskRepository.GetByIdAsync(currentUser.TenantId, id, cancellationToken);
        if (task is null)
        {
            return ApiResponse<string>.Fail("NOT_FOUND", ApiResponseLocalizer.T(HttpContext, "ApprovalTaskNotFound"), HttpContext.TraceIdentifier);
        }

        var request = new Atlas.Application.Approval.Models.ApprovalOperationRequest
        {
            OperationType = ApprovalOperationType.Urge,
            Comment = message
        };

        await _operationService.ExecuteOperationAsync(
            currentUser.TenantId,
            task.InstanceId,
            id,
            currentUser.UserId,
            request,
            cancellationToken);

        return ApiResponse<string>.Ok(ApiResponseLocalizer.T(HttpContext, "ApprovalTaskUrgedSuccess"), HttpContext.TraceIdentifier);
    }

    [HttpPost("{id:long}/communication")]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowUpdate)]
    public async Task<ApiResponse<string>> CommunicateAsync(
        long id,
        [FromQuery] long recipientUserId,
        [FromBody] string content,
        CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();

        var task = await _taskRepository.GetByIdAsync(currentUser.TenantId, id, cancellationToken);
        if (task is null)
        {
            return ApiResponse<string>.Fail("NOT_FOUND", ApiResponseLocalizer.T(HttpContext, "ApprovalTaskNotFound"), HttpContext.TraceIdentifier);
        }

        var request = new Atlas.Application.Approval.Models.ApprovalOperationRequest
        {
            OperationType = ApprovalOperationType.Communicate,
            Comment = content,
            TargetAssigneeValue = recipientUserId.ToString()
        };

        await _operationService.ExecuteOperationAsync(
            currentUser.TenantId,
            task.InstanceId,
            id,
            currentUser.UserId,
            request,
            cancellationToken);

        return ApiResponse<string>.Ok(ApiResponseLocalizer.T(HttpContext, "ApprovalTaskCommunicationSentSuccess"), HttpContext.TraceIdentifier);
    }

    [HttpGet("{id:long}/communications")]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowView)]
    public async Task<ApiResponse<List<ApprovalCommunicationRecord>>> GetCommunicationsAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var records = await _communicationRecordRepository.GetByTaskIdAsync(tenantId, id, cancellationToken);
        return ApiResponse<List<ApprovalCommunicationRecord>>.Ok(records.ToList(), HttpContext.TraceIdentifier);
    }

    [HttpGet("pool")]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowView)]
    public async Task<ApiResponse<PagedResult<ApprovalTaskResponse>>> GetTaskPoolAsync(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var (items, totalCount) = await _taskRepository.GetPagedPoolAsync(tenantId, request.PageIndex, request.PageSize, cancellationToken);
        var result = new PagedResult<ApprovalTaskResponse>(
            _mapper.Map<List<ApprovalTaskResponse>>(items),
            totalCount,
            request.PageIndex,
            request.PageSize);
        return ApiResponse<PagedResult<ApprovalTaskResponse>>.Ok(result, HttpContext.TraceIdentifier);
    }

    [HttpPost("batch-transfer")]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowManage)]
    public async Task<ApiResponse<string>> BatchTransferAsync(
        [FromQuery] long fromUserId,
        [FromQuery] long toUserId,
        CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        if (fromUserId <= 0 || toUserId <= 0 || fromUserId == toUserId)
        {
            throw new BusinessException("VALIDATION_ERROR", "TransferUserInvalid");
        }

        var transferred = await _commandService.BatchTransferTasksAsync(
            currentUser.TenantId,
            fromUserId,
            toUserId,
            currentUser.UserId,
            cancellationToken);

        return ApiResponse<string>.Ok(ApiResponseLocalizer.T(HttpContext, "ApprovalBatchTransferSuccess", transferred), HttpContext.TraceIdentifier);
    }

    [HttpPost("{id:long}/viewed")]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowUpdate)]
    public async Task<ApiResponse<string>> MarkViewedAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var task = await _taskRepository.GetByIdAsync(currentUser.TenantId, id, cancellationToken);
        if (task is null)
        {
            return ApiResponse<string>.Fail("NOT_FOUND", ApiResponseLocalizer.T(HttpContext, "ApprovalTaskNotFound"), HttpContext.TraceIdentifier);
        }

        var isOwnedTask = task.AssigneeType == AssigneeType.User && task.AssigneeValue == currentUser.UserId.ToString();
        if (!isOwnedTask)
        {
            throw new BusinessException("FORBIDDEN", "NoPermissionMarkRead");
        }

        task.MarkViewed(DateTimeOffset.UtcNow);
        await _taskRepository.UpdateAsync(task, cancellationToken);
        return ApiResponse<string>.Ok(ApiResponseLocalizer.T(HttpContext, "ApprovalTaskMarkedViewedSuccess"), HttpContext.TraceIdentifier);
    }

    [HttpPost("{taskId:long}/decision")]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowUpdate)]
    public async Task<ApiResponse<string>> DecideAsync(
        long taskId,
        [FromBody] ApprovalTaskDecideRequest request,
        CancellationToken cancellationToken = default)
    {
        request = request with { TaskId = taskId };
        await _decideValidator.ValidateAndThrowAsync(request, cancellationToken);

        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();

        if (request.Approved)
        {
            await _commandService.ApproveTaskAsync(
                currentUser.TenantId,
                taskId,
                currentUser.UserId,
                request.Comment,
                cancellationToken);
        }
        else
        {
            await _commandService.RejectTaskAsync(
                currentUser.TenantId,
                taskId,
                currentUser.UserId,
                request.Comment,
                cancellationToken);
        }

        var actionName = request.Approved
            ? ApiResponseLocalizer.T(HttpContext, "AuditActionApprovalTaskApprove")
            : ApiResponseLocalizer.T(HttpContext, "AuditActionApprovalTaskReject");
        var resultMessage = request.Approved
            ? ApiResponseLocalizer.T(HttpContext, "ApprovalTaskApprovedSuccess")
            : ApiResponseLocalizer.T(HttpContext, "ApprovalTaskRejectedSuccess");

        var auditContext = new AuditContext(
            currentUser.TenantId,
            currentUser.UserId.ToString(),
            actionName,
            ApiResponseLocalizer.T(HttpContext, "AuditOutcomeSuccess"),
            ApiResponseLocalizer.T(HttpContext, "AuditDetailTaskIdFormat", taskId),
            ControllerHelper.GetIpAddress(HttpContext),
            ControllerHelper.GetUserAgent(HttpContext),
            _clientContextAccessor.GetCurrent());
        await _auditRecorder.RecordAsync(auditContext, cancellationToken);

        return ApiResponse<string>.Ok(resultMessage, HttpContext.TraceIdentifier);
    }
}
