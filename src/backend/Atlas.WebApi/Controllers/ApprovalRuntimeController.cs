using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Models;
using Atlas.Application.Audit.Abstractions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Enums;
using Atlas.Domain.Audit.Entities;
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
    private readonly IValidator<ApprovalStartRequest> _startValidator;
    private readonly IAuditWriter _auditWriter;

    public ApprovalRuntimeController(
        IApprovalRuntimeQueryService queryService,
        IApprovalRuntimeCommandService commandService,
        ITenantProvider tenantProvider,
        IValidator<ApprovalStartRequest> startValidator,
        IAuditWriter auditWriter)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tenantProvider = tenantProvider;
        _startValidator = startValidator;
        _auditWriter = auditWriter;
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

        var tenantId = _tenantProvider.GetTenantId();
        var userId = ControllerHelper.GetUserIdOrThrow(User);

        var result = await _commandService.StartAsync(tenantId, request, userId, cancellationToken);

        // 记录审计日志
        var auditRecord = new AuditRecord(
            tenantId,
            userId.ToString(),
            "审批流程-发起",
            "成功",
            $"流程实例ID: {result.Id}, 流程定义ID: {request.DefinitionId}",
            ControllerHelper.GetIpAddress(HttpContext),
            ControllerHelper.GetUserAgent(HttpContext));
        await _auditWriter.WriteAsync(auditRecord, cancellationToken);

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
        var tenantId = _tenantProvider.GetTenantId();
        var userId = ControllerHelper.GetUserIdOrThrow(User);

        var request = new PagedRequest(pageIndex, pageSize, null, null, false);
        var result = await _queryService.GetInstancesByInitiatorAsync(tenantId, userId, request, status, cancellationToken);
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
        var tenantId = _tenantProvider.GetTenantId();
        var userId = ControllerHelper.GetUserIdOrThrow(User);

        await _commandService.CancelInstanceAsync(tenantId, id, userId, cancellationToken);

        // 记录审计日志
        var auditRecord = new AuditRecord(
            tenantId,
            userId.ToString(),
            "审批流程-取消",
            "成功",
            $"流程实例ID: {id}",
            ControllerHelper.GetIpAddress(HttpContext),
            ControllerHelper.GetUserAgent(HttpContext));
        await _auditWriter.WriteAsync(auditRecord, cancellationToken);

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
        var tenantId = _tenantProvider.GetTenantId();
        var userId = ControllerHelper.GetUserIdOrThrow(User);

        var operationService = HttpContext.RequestServices.GetRequiredService<Atlas.Application.Approval.Abstractions.IApprovalOperationService>();
        await operationService.ExecuteOperationAsync(tenantId, instanceId, taskId, userId, request, cancellationToken);

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
        var auditRecord = new AuditRecord(
            tenantId,
            userId.ToString(),
            $"审批流程-{operationName}",
            "成功",
            $"流程实例ID: {instanceId}, 任务ID: {taskId}",
            ControllerHelper.GetIpAddress(HttpContext),
            ControllerHelper.GetUserAgent(HttpContext));
        await _auditWriter.WriteAsync(auditRecord, cancellationToken);

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
        var tenantId = _tenantProvider.GetTenantId();
        var userId = ControllerHelper.GetUserIdOrThrow(User);

        // 获取实例详情（包含权限校验）
        var instance = await _queryService.GetInstanceByIdAsync(tenantId, id, cancellationToken);
        if (instance == null)
        {
            return ApiResponse<ApprovalInstanceResponse>.Fail(
                "NOT_FOUND",
                "流程实例不存在",
                HttpContext.TraceIdentifier);
        }

        // 权限校验：检查用户是否有权限查看该实例
        // 1. 发起人
        // 2. 审批人（有任务）
        // 3. 抄送人
        // 4. 管理员
        var hasPermission = instance.InitiatorUserId == userId;
        var isAdmin = ControllerHelper.IsInRole(User, "Admin");
        if (!hasPermission)
        {
            // 检查是否是审批人（AssigneeType为User时，AssigneeValue是用户ID字符串）
            var tasks = await _queryService.GetTasksByInstanceAsync(tenantId, id, new PagedRequest(1, 100, null, null, false), cancellationToken);
            hasPermission = tasks.Items.Any(t => 
                t.AssigneeType == Atlas.Domain.Approval.Enums.AssigneeType.User && 
                t.AssigneeValue == userId.ToString());

            // 检查是否是抄送人
            if (!hasPermission)
            {
                var copyRecords = await _queryService.GetMyCopyRecordsAsync(tenantId, userId, new PagedRequest(1, 100, null, null, false), null, cancellationToken);
                hasPermission = copyRecords.Items.Any(c => c.InstanceId == id);
            }
        }

        // 管理员有权限查看所有实例
        if (!hasPermission && isAdmin)
        {
            hasPermission = true;
        }

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
            tenantId,
            id,
            null,
            userId,
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
        var tenantId = _tenantProvider.GetTenantId();
        var userId = ControllerHelper.GetUserIdOrThrow(User);

        // 获取实例详情（包含权限校验）
        var instance = await _queryService.GetInstanceByIdAsync(tenantId, id, cancellationToken);
        if (instance == null)
        {
            return ApiResponse<ApprovalInstanceResponse>.Fail(
                "NOT_FOUND",
                "流程实例不存在",
                HttpContext.TraceIdentifier);
        }

        // 权限校验：检查用户是否有权限查看该实例（与预览相同）
        var hasPermission = instance.InitiatorUserId == userId;
        var isAdmin = ControllerHelper.IsInRole(User, "Admin");
        if (!hasPermission)
        {
            var tasks = await _queryService.GetTasksByInstanceAsync(tenantId, id, new PagedRequest(1, 100, null, null, false), cancellationToken);
            hasPermission = tasks.Items.Any(t => 
                t.AssigneeType == Atlas.Domain.Approval.Enums.AssigneeType.User && 
                t.AssigneeValue == userId.ToString());

            if (!hasPermission)
            {
                var copyRecords = await _queryService.GetMyCopyRecordsAsync(tenantId, userId, new PagedRequest(1, 100, null, null, false), null, cancellationToken);
                hasPermission = copyRecords.Items.Any(c => c.InstanceId == id);
            }
        }

        // 管理员有权限查看所有实例
        if (!hasPermission && isAdmin)
        {
            hasPermission = true;
        }

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
            tenantId,
            id,
            null,
            userId,
            Atlas.Domain.Approval.Enums.ApprovalOperationType.Print,
            cancellationToken);

        return ApiResponse<ApprovalInstanceResponse>.Ok(instance, HttpContext.TraceIdentifier);
    }
}
