using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Models;
using Atlas.Application.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using FluentValidation;

namespace Atlas.WebApi.Controllers;

/// <summary>
/// 审批流部门负责人管理控制器
/// </summary>
[ApiController]
[Route("api/v1/approval/department-leaders")]
[Authorize(Policy = PermissionPolicies.SystemAdmin)]
public sealed class ApprovalDepartmentLeadersController : ControllerBase
{
    private readonly IApprovalDepartmentLeaderService _leaderService;
    private readonly ITenantProvider _tenantProvider;
    private readonly IValidator<ApprovalDepartmentLeaderRequest> _validator;

    public ApprovalDepartmentLeadersController(
        IApprovalDepartmentLeaderService leaderService,
        ITenantProvider tenantProvider,
        IValidator<ApprovalDepartmentLeaderRequest> validator)
    {
        _leaderService = leaderService;
        _tenantProvider = tenantProvider;
        _validator = validator;
    }

    /// <summary>
    /// 设置部门负责人
    /// </summary>
    [HttpPost]
    public async Task<ApiResponse<string>> SetLeaderAsync(
        ApprovalDepartmentLeaderRequest request,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var tenantId = _tenantProvider.GetTenantId();
        await _leaderService.SetLeaderAsync(tenantId, request, cancellationToken);
        return ApiResponse<string>.Ok("已设置", HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// 获取部门负责人
    /// </summary>
    [HttpGet("{departmentId}")]
    public async Task<ApiResponse<long?>> GetLeaderAsync(
        long departmentId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _leaderService.GetLeaderUserIdAsync(tenantId, departmentId, cancellationToken);
        return ApiResponse<long?>.Ok(result, HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// 移除部门负责人
    /// </summary>
    [HttpDelete("{departmentId}")]
    public async Task<ApiResponse<string>> RemoveLeaderAsync(
        long departmentId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _leaderService.RemoveLeaderAsync(tenantId, departmentId, cancellationToken);
        return ApiResponse<string>.Ok("已移除", HttpContext.TraceIdentifier);
    }
}
