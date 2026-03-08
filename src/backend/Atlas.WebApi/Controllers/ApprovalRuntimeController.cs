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
using Atlas.WebApi.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Atlas.WebApi.Helpers;
using System.Text;
using Atlas.Application.Identity.Abstractions;

namespace Atlas.WebApi.Controllers;

/// <summary>
/// 审批流运行时控制器（发起、我的发起、实例查询等）
/// </summary>
[ApiController]
[Route("api/v1/approval/instances")]
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
    private readonly IRbacResolver _rbacResolver;

    public ApprovalRuntimeController(
        IApprovalRuntimeQueryService queryService,
        IApprovalRuntimeCommandService commandService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IClientContextAccessor clientContextAccessor,
        IValidator<ApprovalStartRequest> startValidator,
        IAuditRecorder auditRecorder,
        IRbacResolver rbacResolver)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _clientContextAccessor = clientContextAccessor;
        _startValidator = startValidator;
        _auditRecorder = auditRecorder;
        _rbacResolver = rbacResolver;
    }

    /// <summary>
    /// 发起流程实例
    /// </summary>
    [HttpPost]
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
    [HttpGet("my")]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowView)]
    public async Task<ApiResponse<PagedResult<ApprovalInstanceListItem>>> GetMyInstancesAsync(
        [FromQuery] PagedRequest request,
        [FromQuery] ApprovalInstanceStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var result = await _queryService.GetInstancesByInitiatorAsync(
            currentUser.TenantId,
            currentUser.UserId,
            request,
            status,
            cancellationToken);
        return ApiResponse<PagedResult<ApprovalInstanceListItem>>.Ok(result, HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// 管理端查询流程实例
    /// </summary>
    [HttpGet("admin")]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowView)]
    public async Task<ApiResponse<PagedResult<ApprovalInstanceListItem>>> GetAdminInstancesAsync(
        [FromQuery] PagedRequest request,
        [FromQuery] long? definitionId = null,
        [FromQuery] long? initiatorUserId = null,
        [FromQuery] string? businessKey = null,
        [FromQuery] DateTimeOffset? startedFrom = null,
        [FromQuery] DateTimeOffset? startedTo = null,
        [FromQuery] ApprovalInstanceStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var isAdmin = await IsAdminAsync(currentUser, cancellationToken);
        if (!isAdmin)
        {
            return ApiResponse<PagedResult<ApprovalInstanceListItem>>.Fail(
                "FORBIDDEN",
                "仅管理员可访问管理端流程实例查询",
                HttpContext.TraceIdentifier);
        }

        var result = await _queryService.GetInstancesPagedAsync(
            currentUser.TenantId,
            request,
            definitionId,
            initiatorUserId,
            startedFrom,
            startedTo,
            businessKey,
            status,
            cancellationToken);
        return ApiResponse<PagedResult<ApprovalInstanceListItem>>.Ok(result, HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// 获取流程实例详情
    /// </summary>
    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowView)]
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
    [HttpGet("{id:long}/history")]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowView)]
    public async Task<ApiResponse<PagedResult<ApprovalHistoryEventResponse>>> GetHistoryAsync(
        long id,
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetHistoryAsync(tenantId, id, request, cancellationToken);
        return ApiResponse<PagedResult<ApprovalHistoryEventResponse>>.Ok(result, HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// 导出流程实例历史事件（CSV）
    /// </summary>
    [HttpGet("{id:long}/history/export")]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowView)]
    public async Task<IActionResult> ExportHistoryAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var request = new PagedRequest(1, 5000, null, null, false);
        var result = await _queryService.GetHistoryAsync(tenantId, id, request, cancellationToken);

        var csv = new StringBuilder();
        csv.AppendLine("Id,EventType,FromNode,ToNode,ActorUserId,OccurredAt");
        foreach (var item in result.Items)
        {
            csv.Append(item.Id).Append(',')
                .Append(EscapeCsv(item.EventType)).Append(',')
                .Append(EscapeCsv(item.FromNode)).Append(',')
                .Append(EscapeCsv(item.ToNode)).Append(',')
                .Append(item.ActorUserId?.ToString() ?? string.Empty).Append(',')
                .Append(item.OccurredAt.ToString("O"))
                .AppendLine();
        }

        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var auditContext = new AuditContext(
            currentUser.TenantId,
            currentUser.UserId.ToString(),
            "审批流程-导出历史",
            "成功",
            $"流程实例ID: {id}",
            ControllerHelper.GetIpAddress(HttpContext),
            ControllerHelper.GetUserAgent(HttpContext),
            _clientContextAccessor.GetCurrent());
        await _auditRecorder.RecordAsync(auditContext, cancellationToken);

        var bytes = Encoding.UTF8.GetBytes(csv.ToString());
        var fileName = $"approval-history-{id}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.csv";
        return File(bytes, "text/csv; charset=utf-8", fileName);
    }

    /// <summary>
    /// 取消流程实例
    /// </summary>
    [HttpPost("{id:long}/cancellation")]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowView)]
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
    /// 挂起流程实例
    /// </summary>
    [HttpPost("{id:long}/suspension")]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowView)]
    public async Task<ApiResponse<string>> SuspendAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();

        await _commandService.SuspendInstanceAsync(
            currentUser.TenantId,
            id,
            currentUser.UserId,
            cancellationToken);

        // 记录审计日志
        var auditContext = new AuditContext(
            currentUser.TenantId,
            currentUser.UserId.ToString(),
            "审批流程-挂起",
            "成功",
            $"流程实例ID: {id}",
            ControllerHelper.GetIpAddress(HttpContext),
            ControllerHelper.GetUserAgent(HttpContext),
            _clientContextAccessor.GetCurrent());
        await _auditRecorder.RecordAsync(auditContext, cancellationToken);

        return ApiResponse<string>.Ok("已挂起", HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// 激活流程实例
    /// </summary>
    [HttpPost("{id:long}/activation")]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowView)]
    public async Task<ApiResponse<string>> ActivateAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();

        await _commandService.ActivateInstanceAsync(
            currentUser.TenantId,
            id,
            currentUser.UserId,
            cancellationToken);

        // 记录审计日志
        var auditContext = new AuditContext(
            currentUser.TenantId,
            currentUser.UserId.ToString(),
            "审批流程-激活",
            "成功",
            $"流程实例ID: {id}",
            ControllerHelper.GetIpAddress(HttpContext),
            ControllerHelper.GetUserAgent(HttpContext),
            _clientContextAccessor.GetCurrent());
        await _auditRecorder.RecordAsync(auditContext, cancellationToken);

        return ApiResponse<string>.Ok("已激活", HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// 强制终止流程实例（管理员操作）
    /// </summary>
    [HttpPost("{id:long}/termination")]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowManage)]
    public async Task<ApiResponse<string>> TerminateAsync(
        long id,
        [FromBody] string? comment,
        CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();

        await _commandService.TerminateInstanceAsync(
            currentUser.TenantId,
            id,
            currentUser.UserId,
            comment,
            cancellationToken);

        // 记录审计日志
        var auditContext = new AuditContext(
            currentUser.TenantId,
            currentUser.UserId.ToString(),
            "审批流程-终止",
            "成功",
            $"流程实例ID: {id}",
            ControllerHelper.GetIpAddress(HttpContext),
            ControllerHelper.GetUserAgent(HttpContext),
            _clientContextAccessor.GetCurrent());
        await _auditRecorder.RecordAsync(auditContext, cancellationToken);

        return ApiResponse<string>.Ok("已终止", HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// 暂存草稿
    /// </summary>
    [HttpPost("draft")]
    public async Task<ApiResponse<ApprovalInstanceResponse>> SaveDraftAsync(
        ApprovalStartRequest request,
        CancellationToken cancellationToken = default)
    {
        // 草稿不需要完整的启动校验，但基础字段需要校验
        // await _startValidator.ValidateAndThrowAsync(request, cancellationToken);

        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();

        var result = await _commandService.SaveDraftAsync(
            currentUser.TenantId,
            request,
            currentUser.UserId,
            cancellationToken);

        return ApiResponse<ApprovalInstanceResponse>.Ok(result, HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// 提交草稿（激活流程）
    /// </summary>
    [HttpPost("{id:long}/submission")]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowView)]
    public async Task<ApiResponse<ApprovalInstanceResponse>> SubmitDraftAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();

        var result = await _commandService.SubmitDraftAsync(
            currentUser.TenantId,
            id,
            currentUser.UserId,
            cancellationToken);

        // 记录审计日志
        var auditContext = new AuditContext(
            currentUser.TenantId,
            currentUser.UserId.ToString(),
            "审批流程-提交草稿",
            "成功",
            $"流程实例ID: {result.Id}",
            ControllerHelper.GetIpAddress(HttpContext),
            ControllerHelper.GetUserAgent(HttpContext),
            _clientContextAccessor.GetCurrent());
        await _auditRecorder.RecordAsync(auditContext, cancellationToken);

        return ApiResponse<ApprovalInstanceResponse>.Ok(result, HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// 执行运行时操作（撤回、转办、加签、打回修改、退回任意节点、撤销同意等）
    /// </summary>
    [HttpPost("{instanceId:long}/operations")]
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
    [HttpGet("{id:long}/preview")]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowView)]
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
    [HttpGet("{id:long}/print-view")]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowView)]
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
        var isAdmin = await IsAdminAsync(currentUser, cancellationToken);
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

    private async Task<bool> IsAdminAsync(CurrentUserInfo currentUser, CancellationToken cancellationToken)
    {
        var roleCodes = await _rbacResolver.GetRoleCodesAsync(
            currentUser.TenantId,
            currentUser.UserId,
            cancellationToken);
        return roleCodes.Contains("Admin", StringComparer.OrdinalIgnoreCase)
            || roleCodes.Contains("SuperAdmin", StringComparer.OrdinalIgnoreCase);
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }
}
