using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Models;
using Atlas.Application.Audit.Abstractions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Enums;
using Atlas.Domain.Audit.Entities;
using FluentValidation;
using Atlas.WebApi.Helpers;

namespace Atlas.WebApi.Controllers;

/// <summary>
/// 审批任务控制器（我的待办、审批/驳回等）
/// </summary>
[ApiController]
[Route("api/approval/tasks")]
[Authorize]
public sealed class ApprovalTasksController : ControllerBase
{
    private readonly IApprovalRuntimeQueryService _queryService;
    private readonly IApprovalRuntimeCommandService _commandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IValidator<ApprovalTaskDecideRequest> _decideValidator;
    private readonly IAuditWriter _auditWriter;

    public ApprovalTasksController(
        IApprovalRuntimeQueryService queryService,
        IApprovalRuntimeCommandService commandService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IValidator<ApprovalTaskDecideRequest> decideValidator,
        IAuditWriter auditWriter)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _decideValidator = decideValidator;
        _auditWriter = auditWriter;
    }

    /// <summary>
    /// 获取我的待办任务
    /// </summary>
    [HttpGet("my-tasks")]
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
    [HttpGet("by-instance/{instanceId}")]
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
    [HttpPost("{taskId}/approve")]
    public async Task<ApiResponse<string>> ApproveAsync(
        long taskId,
        [FromBody] ApprovalTaskDecideRequest request,
        CancellationToken cancellationToken = default)
    {
        request = request with { TaskId = taskId, Approved = true };
        await _decideValidator.ValidateAndThrowAsync(request, cancellationToken);

        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();

        await _commandService.ApproveTaskAsync(
            currentUser.TenantId,
            taskId,
            currentUser.UserId,
            request.Comment,
            cancellationToken);

        // 记录审计日志
        var clientContext = ControllerHelper.GetClientContext(HttpContext);
        var auditRecord = new AuditRecord(
            currentUser.TenantId,
            currentUser.UserId.ToString(),
            "审批任务-同意",
            "成功",
            $"任务ID: {taskId}",
            ControllerHelper.GetIpAddress(HttpContext),
            ControllerHelper.GetUserAgent(HttpContext),
            clientContext.ClientType.ToString(),
            clientContext.ClientPlatform.ToString(),
            clientContext.ClientChannel.ToString(),
            clientContext.ClientAgent.ToString());
        await _auditWriter.WriteAsync(auditRecord, cancellationToken);

        return ApiResponse<string>.Ok("已同意", HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// 驳回任务（审批不通过）
    /// </summary>
    [HttpPost("{taskId}/reject")]
    public async Task<ApiResponse<string>> RejectAsync(
        long taskId,
        [FromBody] ApprovalTaskDecideRequest request,
        CancellationToken cancellationToken = default)
    {
        request = request with { TaskId = taskId, Approved = false };
        await _decideValidator.ValidateAndThrowAsync(request, cancellationToken);

        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();

        await _commandService.RejectTaskAsync(
            currentUser.TenantId,
            taskId,
            currentUser.UserId,
            request.Comment,
            cancellationToken);

        // 记录审计日志
        var clientContext = ControllerHelper.GetClientContext(HttpContext);
        var auditRecord = new AuditRecord(
            currentUser.TenantId,
            currentUser.UserId.ToString(),
            "审批任务-驳回",
            "成功",
            $"任务ID: {taskId}",
            ControllerHelper.GetIpAddress(HttpContext),
            ControllerHelper.GetUserAgent(HttpContext),
            clientContext.ClientType.ToString(),
            clientContext.ClientPlatform.ToString(),
            clientContext.ClientChannel.ToString(),
            clientContext.ClientAgent.ToString());
        await _auditWriter.WriteAsync(auditRecord, cancellationToken);

        return ApiResponse<string>.Ok("已驳回", HttpContext.TraceIdentifier);
    }
}
