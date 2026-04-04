using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Models;
using Atlas.Application.Approval.Repositories;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Audit.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.WebApi.Authorization;
using Atlas.WebApi.Helpers;
using Atlas.WebApi.Filters;

namespace Atlas.WebApi.Controllers;

/// <summary>
/// 审批代理人控制器
/// </summary>
[ApiController]
[Route("api/v1/approval/agents")]
[Authorize]
[PlatformOnly]
public sealed class ApprovalAgentController : ControllerBase
{
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IApprovalAgentConfigRepository _agentConfigRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IAuditRecorder _auditRecorder;
    private readonly IClientContextAccessor _clientContextAccessor;

    public ApprovalAgentController(
        ICurrentUserAccessor currentUserAccessor,
        IApprovalAgentConfigRepository agentConfigRepository,
        IIdGeneratorAccessor idGeneratorAccessor,
        IAuditRecorder auditRecorder,
        IClientContextAccessor clientContextAccessor)
    {
        _currentUserAccessor = currentUserAccessor;
        _agentConfigRepository = agentConfigRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
        _auditRecorder = auditRecorder;
        _clientContextAccessor = clientContextAccessor;
    }

    /// <summary>
    /// 获取我的代理设置
    /// </summary>
    [HttpGet]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowView)]
    public async Task<ApiResponse<List<ApprovalAgentConfig>>> GetMyAgentsAsync(CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var configs = await _agentConfigRepository.GetByPrincipalUserIdAsync(
            currentUser.TenantId,
            currentUser.UserId,
            cancellationToken);
        return ApiResponse<List<ApprovalAgentConfig>>.Ok(configs.ToList(), HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// 创建代理设置
    /// </summary>
    [HttpPost]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowUpdate)]
    public async Task<ApiResponse<string>> CreateAgentAsync(
        [FromBody] CreateApprovalAgentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.EndTime <= request.StartTime)
        {
            throw new BusinessException("VALIDATION_ERROR", "AgentEndTimeInvalid");
        }

        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var config = new ApprovalAgentConfig(
            currentUser.TenantId,
            request.AgentUserId,
            currentUser.UserId,
            request.StartTime,
            request.EndTime,
            _idGeneratorAccessor.NextId());
        await _agentConfigRepository.AddAsync(config, cancellationToken);

        var auditContext = new AuditContext(
            currentUser.TenantId,
            currentUser.UserId.ToString(),
            ApiResponseLocalizer.T(HttpContext, "AuditActionApprovalAgentCreate"),
            ApiResponseLocalizer.T(HttpContext, "AuditOutcomeSuccess"),
            ApiResponseLocalizer.T(HttpContext, "AuditDetailApprovalAgentConfigIdFormat", config.Id),
            ControllerHelper.GetIpAddress(HttpContext),
            ControllerHelper.GetUserAgent(HttpContext),
            _clientContextAccessor.GetCurrent());
        await _auditRecorder.RecordAsync(auditContext, cancellationToken);
        return ApiResponse<string>.Ok(ApiResponseLocalizer.T(HttpContext, "ApprovalAgentCreatedSuccess"), HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// 删除代理设置
    /// </summary>
    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowUpdate)]
    public async Task<ApiResponse<string>> DeleteAgentAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var config = await _agentConfigRepository.GetByIdAsync(currentUser.TenantId, id, cancellationToken);
        if (config is null)
        {
            return ApiResponse<string>.Fail("NOT_FOUND", ApiResponseLocalizer.T(HttpContext, "ApprovalAgentConfigNotFound"), HttpContext.TraceIdentifier);
        }

        if (config.PrincipalUserId != currentUser.UserId)
        {
            throw new BusinessException("FORBIDDEN", "AgentForbiddenDelete");
        }

        await _agentConfigRepository.DeleteAsync(currentUser.TenantId, id, cancellationToken);

        var auditContext = new AuditContext(
            currentUser.TenantId,
            currentUser.UserId.ToString(),
            ApiResponseLocalizer.T(HttpContext, "AuditActionApprovalAgentDelete"),
            ApiResponseLocalizer.T(HttpContext, "AuditOutcomeSuccess"),
            ApiResponseLocalizer.T(HttpContext, "AuditDetailApprovalAgentConfigIdFormat", id),
            ControllerHelper.GetIpAddress(HttpContext),
            ControllerHelper.GetUserAgent(HttpContext),
            _clientContextAccessor.GetCurrent());
        await _auditRecorder.RecordAsync(auditContext, cancellationToken);
        return ApiResponse<string>.Ok(ApiResponseLocalizer.T(HttpContext, "ApprovalFlowDeleteSuccess"), HttpContext.TraceIdentifier);
    }

    public sealed record CreateApprovalAgentRequest
    {
        public required long AgentUserId { get; init; }
        public required DateTimeOffset StartTime { get; init; }
        public required DateTimeOffset EndTime { get; init; }
    }
}
