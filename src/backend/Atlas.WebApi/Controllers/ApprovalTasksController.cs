using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Models;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Audit.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Enums;
using FluentValidation;
using Atlas.WebApi.Helpers;

namespace Atlas.WebApi.Controllers;

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
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IClientContextAccessor _clientContextAccessor;
    private readonly IValidator<ApprovalTaskDecideRequest> _decideValidator;
    private readonly IAuditRecorder _auditRecorder;

    public ApprovalTasksController(
        IApprovalRuntimeQueryService queryService,
        IApprovalRuntimeCommandService commandService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IClientContextAccessor clientContextAccessor,
        IValidator<ApprovalTaskDecideRequest> decideValidator,
        IAuditRecorder auditRecorder)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _clientContextAccessor = clientContextAccessor;
        _decideValidator = decideValidator;
        _auditRecorder = auditRecorder;
    }

    /// <summary>
    /// 获取我的待办任务
    /// </summary>
    [HttpGet("my")]
    public async Task<ApiResponse<PagedResult<ApprovalTaskResponse>>> GetMyTasksAsync(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] ApprovalTaskStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();

        var request = new PagedRequest(pageIndex, pageSize, null, null, false);
        var result = await _queryService.GetMyTasksAsync(
            currentUser.TenantId,
            currentUser.UserId,
            request,
            status,
            cancellationToken);
        return ApiResponse<PagedResult<ApprovalTaskResponse>>.Ok(result, HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// 获取实例内的所有任务
    /// </summary>
    [HttpGet("~/api/v1/approval/instances/{instanceId:long}/tasks")]
    public async Task<ApiResponse<PagedResult<ApprovalTaskResponse>>> GetByInstanceAsync(
        long instanceId,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var request = new PagedRequest(pageIndex, pageSize, null, null, false);
        var result = await _queryService.GetTasksByInstanceAsync(tenantId, instanceId, request, cancellationToken);
        return ApiResponse<PagedResult<ApprovalTaskResponse>>.Ok(result, HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// 同意任务（审批通过）
    /// </summary>
    [HttpPost("{taskId:long}/decision")]
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

        var actionName = request.Approved ? "审批任务-同意" : "审批任务-驳回";
        var resultMessage = request.Approved ? "已同意" : "已驳回";

        var auditContext = new AuditContext(
            currentUser.TenantId,
            currentUser.UserId.ToString(),
            actionName,
            "成功",
            $"任务ID: {taskId}",
            ControllerHelper.GetIpAddress(HttpContext),
            ControllerHelper.GetUserAgent(HttpContext),
            _clientContextAccessor.GetCurrent());
        await _auditRecorder.RecordAsync(auditContext, cancellationToken);

        return ApiResponse<string>.Ok(resultMessage, HttpContext.TraceIdentifier);
    }
}
