using Atlas.Application.System.Models;
using Atlas.Application.System.Abstractions;
using Atlas.Core.Models;
using Atlas.Presentation.Shared.Authorization;
using Atlas.Presentation.Shared.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.PlatformHost.Controllers;

/// <summary>
/// 租户管理接口
/// </summary>
[ApiController]
[Route("api/v1/tenants")]
[Authorize]
public sealed class TenantsController : ControllerBase
{
    private readonly ITenantService _tenantService;

    public TenantsController(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    /// <summary>
    /// 分页查询租户列表
    /// </summary>
    [HttpGet]
    [Authorize(Policy = PermissionPolicies.TenantsView)]
    public async Task<ActionResult<ApiResponse<PagedResult<TenantDto>>>> GetPaged(
        [FromQuery] TenantQueryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _tenantService.GetPagedAsync(request, cancellationToken);
        return Ok(ApiResponse<PagedResult<TenantDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 获取单个租户详情
    /// </summary>
    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.TenantsView)]
    public async Task<ActionResult<ApiResponse<TenantDto>>> GetById(
        long id,
        CancellationToken cancellationToken)
    {
        var result = await _tenantService.GetByIdAsync(id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<TenantDto>.Fail("NOT_FOUND", ApiResponseLocalizer.T(HttpContext, "TenantNotFound"), HttpContext.TraceIdentifier));
        }
        return Ok(ApiResponse<TenantDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 创建租户
    /// </summary>
    [HttpPost]
    [Authorize(Policy = PermissionPolicies.TenantsCreate)]
    public async Task<ActionResult<ApiResponse<long>>> Create(
        [FromBody] TenantCreateRequest request,
        CancellationToken cancellationToken)
    {
        var userId = ControllerHelper.GetUserIdOrThrow(User);
        var id = await _tenantService.CreateAsync(userId, request, cancellationToken);
        return Ok(ApiResponse<long>.Ok(id, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 更新租户
    /// </summary>
    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.TenantsUpdate)]
    public async Task<ActionResult<ApiResponse<bool>>> Update(
        long id,
        [FromBody] TenantUpdateRequest request,
        CancellationToken cancellationToken)
    {
        // 确保路由的ID跟请求体的ID一致
        var updateRequest = request with { Id = id };
        var userId = ControllerHelper.GetUserIdOrThrow(User);
        await _tenantService.UpdateAsync(userId, updateRequest, cancellationToken);
        return Ok(ApiResponse<bool>.Ok(true, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 启用/停用租户
    /// </summary>
    [HttpPatch("{id:long}/status")]
    [Authorize(Policy = PermissionPolicies.TenantsUpdate)]
    public async Task<ActionResult<ApiResponse<bool>>> ToggleStatus(
        long id,
        [FromBody] ToggleStatusRequest request,
        CancellationToken cancellationToken)
    {
        var userId = ControllerHelper.GetUserIdOrThrow(User);
        await _tenantService.ToggleStatusAsync(userId, id, request.IsActive, cancellationToken);
        return Ok(ApiResponse<bool>.Ok(true, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 删除租户
    /// </summary>
    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.TenantsDelete)]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(
        long id,
        CancellationToken cancellationToken)
    {
        var userId = ControllerHelper.GetUserIdOrThrow(User);
        await _tenantService.DeleteAsync(userId, id, cancellationToken);
        return Ok(ApiResponse<bool>.Ok(true, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 续期租户（更新到期时间，自动恢复激活状态）
    /// </summary>
    [HttpPost("{id:long}/renew")]
    [Authorize(Policy = PermissionPolicies.TenantsUpdate)]
    public async Task<ActionResult<ApiResponse<bool>>> Renew(
        long id,
        [FromBody] TenantRenewRequest request,
        CancellationToken cancellationToken)
    {
        var userId = ControllerHelper.GetUserIdOrThrow(User);
        await _tenantService.RenewAsync(userId, id, request.NewExpiredAt, cancellationToken);
        return Ok(ApiResponse<bool>.Ok(true, HttpContext.TraceIdentifier));
    }
}

public sealed record ToggleStatusRequest(bool IsActive);
