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
using Microsoft.Extensions.DependencyInjection;
using Atlas.WebApi.Helpers;

namespace Atlas.WebApi.Controllers;

/// <summary>
/// 审批流运行时控制器（发起、我的发起、实例查询等）
/// </summary>
[ApiController]
[Route("api/approval/runtime")]
[Authorize]
public sealed class ApprovalRuntimeController : ControllerBase
{
    private readonly IApprovalRuntimeQueryService _queryService;
    private readonly IApprovalRuntimeCommandService _commandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IClientContextAccessor _clientContextAccessor;
    private readonly IValidator<ApprovalStartRequest> _startValidator;
    private readonly IAuditRecorder _auditRecorder;

    public ApprovalRuntimeController(
        IApprovalRuntimeQueryService queryService,
        IApprovalRuntimeCommandService commandService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IClientContextAccessor clientContextAccessor,
        IValidator<ApprovalStartRequest> startValidator,
        IAuditRecorder auditRecorder)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _clientContextAccessor = clientContextAccessor;
        _startValidator = startValidator;
        _auditRecorder = auditRecorder;
    }

    /// <summary>
    /// 发起流程实例
    /// </summary>
    [HttpPost("start")]
    public async Task<ApiResponse<ApprovalInstanceResponse>> StartAsync(
        ApprovalStartRequest request,
        CancellationToken cancellationToken = default)
    {
        await _startValidator.ValidateAndThrowAsync(request, cancellationToken);

        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();

        var result = await _commandService.StartAsync(
            currentUser.TenantId,
            request,
            currentUser.UserId,
            cancellationToken);

        // 记录审计日志
        var auditContext = new AuditContext(
            currentUser.TenantId,
            currentUser.UserId.ToString(),
            "审批流程-发起",
            "成功",
            $"流程实例ID: {result.Id}, 流程定义ID: {request.DefinitionId}",
            ControllerHelper.GetIpAddress(HttpContext),
            ControllerHelper.GetUserAgent(HttpContext),
            _clientContextAccessor.GetCurrent());
        await _auditRecorder.RecordAsync(auditContext, cancellationToken);

        return ApiResponse<ApprovalInstanceResponse>.Ok(result, HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// 获取我发起的流程实例
    /// </summary>
    [HttpGet("my-instances")]
    public async Task<ApiResponse<PagedResult<ApprovalInstanceListItem>>> GetMyInstancesAsync(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] ApprovalInstanceStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();

        var request = new PagedRequest(pageIndex, pageSize, null, null, false);
        var result = await _queryService.GetInstancesByInitiatorAsync(
            currentUser.TenantId,
            currentUser.UserId,
            request,
            status,
            cancellationToken);
        return ApiResponse<PagedResult<ApprovalInstanceListItem>>.Ok(result, HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// 获取流程实例详情
    /// </summary>
    [HttpGet("instances/{id}")]
    public async Task<ApiResponse<ApprovalInstanceResponse>> GetInstanceByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetInstanceByIdAsync(tenantId, id, cancellationToken);
        if (result == null)
        {
            return ApiResponse<ApprovalInstanceResponse>.Fail(
                "NOT_FOUND",
                "流程实例不存在",
                HttpContext.TraceIdentifier);
        }

        return ApiResponse<ApprovalInstanceResponse>.Ok(result, HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// 获取流程实例的历史事件
    /// </summary>
    [HttpGet("instances/{id}/history")]
    public async Task<ApiResponse<PagedResult<ApprovalHistoryEventResponse>>> GetHistoryAsync(
        long id,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var request = new PagedRequest(pageIndex, pageSize, null, null, false);
        var result = await _queryService.GetHistoryAsync(tenantId, id, request, cancellationToken);
        return ApiResponse<PagedResult<ApprovalHistoryEventResponse>>.Ok(result, HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// 取消流程实例
    /// </summary>
    [HttpPost("instances/{id}/cancel")]
    public async Task<ApiResponse<string>> CancelAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();

        await _commandService.CancelInstanceAsync(
            currentUser.TenantId,
            id,
            currentUser.UserId,
            cancellationToken);

        // 记录审计日志
        var auditContext = new AuditContext(
            currentUser.TenantId,
            currentUser.UserId.ToString(),
            "审批流程-取消",
            "成功",
            $"流程实例ID: {id}",
            ControllerHelper.GetIpAddress(HttpContext),
            ControllerHelper.GetUserAgent(HttpContext),
            _clientContextAccessor.GetCurrent());
        await _auditRecorder.RecordAsync(auditContext, cancellationToken);

        return ApiResponse<string>.Ok("已取消", HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// 执行运行时操作（撤回、转办、加签、打回修改、退回任意节点、撤销同意等）
    /// </summary>
    [HttpPost("instances/{instanceId}/operations")]
    public async Task<ApiResponse<string>> ExecuteOperationAsync(
        long instanceId,
        [FromBody] Atlas.Application.Approval.Models.ApprovalOperationRequest request,
        [FromQuery] long? taskId = null,
        CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();

        var operationService = HttpContext.RequestServices.GetRequiredService<Atlas.Application.Approval.Abstractions.IApprovalOperationService>();
        await operationService.ExecuteOperationAsync(
            currentUser.TenantId,
            instanceId,
            taskId,
            currentUser.UserId,
            request,
            cancellationToken);

        var operationName = request.OperationType switch
        {
            Atlas.Domain.Approval.Enums.ApprovalOperationType.ProcessDrawBack => "撤回",
            Atlas.Domain.Approval.Enums.ApprovalOperationType.Transfer => "转办",
            Atlas.Domain.Approval.Enums.ApprovalOperationType.AddAssignee => "加签",
            Atlas.Domain.Approval.Enums.ApprovalOperationType.BackToModify => "打回修改",
            Atlas.Domain.Approval.Enums.ApprovalOperationType.BackToAnyNode => "退回任意节点",
            Atlas.Domain.Approval.Enums.ApprovalOperationType.DrawBackAgree => "撤销同意",
            _ => "操作"
        };

        // 记录审计日志
        var auditContext = new AuditContext(
            currentUser.TenantId,
            currentUser.UserId.ToString(),
            $"审批流程-{operationName}",
            "成功",
            $"流程实例ID: {instanceId}, 任务ID: {taskId}",
            ControllerHelper.GetIpAddress(HttpContext),
            ControllerHelper.GetUserAgent(HttpContext),
            _clientContextAccessor.GetCurrent());
        await _auditRecorder.RecordAsync(auditContext, cancellationToken);

        return ApiResponse<string>.Ok($"{operationName}成功", HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// 预览流程实例（需要权限校验和审计记录）
    /// </summary>
    [HttpGet("instances/{id}/preview")]
    public async Task<ApiResponse<ApprovalInstanceResponse>> PreviewInstanceAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();

        // 获取实例详情（包含权限校验）
        var instance = await _queryService.GetInstanceByIdAsync(currentUser.TenantId, id, cancellationToken);
        if (instance == null)
        {
            return ApiResponse<ApprovalInstanceResponse>.Fail(
                "NOT_FOUND",
                "流程实例不存在",
                HttpContext.TraceIdentifier);
        }

        var hasPermission = await HasInstanceAccessAsync(currentUser, id, instance, cancellationToken);
        if (!hasPermission)
        {
            return ApiResponse<ApprovalInstanceResponse>.Fail(
                "FORBIDDEN",
                "您没有权限查看该流程实例",
                HttpContext.TraceIdentifier);
        }

        // 记录审计日志
        var operationService = HttpContext.RequestServices.GetRequiredService<Atlas.Application.Approval.Abstractions.IApprovalOperationService>();
        await operationService.RecordUiOperationAsync(
            currentUser.TenantId,
            id,
            null,
            currentUser.UserId,
            Atlas.Domain.Approval.Enums.ApprovalOperationType.Preview,
            cancellationToken);

        return ApiResponse<ApprovalInstanceResponse>.Ok(instance, HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// 打印流程实例（需要权限校验和审计记录）
    /// </summary>
    [HttpGet("instances/{id}/print")]
    public async Task<ApiResponse<ApprovalInstanceResponse>> PrintInstanceAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();

        // 获取实例详情（包含权限校验）
        var instance = await _queryService.GetInstanceByIdAsync(currentUser.TenantId, id, cancellationToken);
        if (instance == null)
        {
            return ApiResponse<ApprovalInstanceResponse>.Fail(
                "NOT_FOUND",
                "流程实例不存在",
                HttpContext.TraceIdentifier);
        }

        var hasPermission = await HasInstanceAccessAsync(currentUser, id, instance, cancellationToken);
        if (!hasPermission)
        {
            return ApiResponse<ApprovalInstanceResponse>.Fail(
                "FORBIDDEN",
                "您没有权限打印该流程实例",
                HttpContext.TraceIdentifier);
        }

        // 记录审计日志
        var operationService = HttpContext.RequestServices.GetRequiredService<Atlas.Application.Approval.Abstractions.IApprovalOperationService>();
        await operationService.RecordUiOperationAsync(
            currentUser.TenantId,
            id,
            null,
            currentUser.UserId,
            Atlas.Domain.Approval.Enums.ApprovalOperationType.Print,
            cancellationToken);

        return ApiResponse<ApprovalInstanceResponse>.Ok(instance, HttpContext.TraceIdentifier);
    }

    private async Task<bool> HasInstanceAccessAsync(
        CurrentUserInfo currentUser,
        long instanceId,
        ApprovalInstanceResponse instance,
        CancellationToken cancellationToken)
    {
        var isAdmin = currentUser.Roles.Contains("Admin", StringComparer.OrdinalIgnoreCase);
        if (isAdmin)
        {
            return true;
        }

        if (instance.InitiatorUserId == currentUser.UserId)
        {
            return true;
        }

        return await _queryService.HasInstanceAccessAsync(
            currentUser.TenantId,
            instanceId,
            currentUser.UserId,
            cancellationToken);
    }
}
