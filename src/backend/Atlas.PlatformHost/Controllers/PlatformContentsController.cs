using Atlas.Application.Coze.Abstractions;
using Atlas.Application.Coze.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers;

/// <summary>
/// 平台运营内容 CRUD（M5.3）。供运营后台维护首页 / 社区 / 通用管理 / 模板插件摘要等内容。
/// 读取沿用普通用户权限（首页等公开展示），写入仅 SystemAdmin。
/// </summary>
[ApiController]
[Route("api/v1/platform/contents")]
[Authorize]
public sealed class PlatformContentsController : ControllerBase
{
    private readonly IPlatformContentAdminService _service;
    private readonly ITenantProvider _tenantProvider;

    public PlatformContentsController(
        IPlatformContentAdminService service,
        ITenantProvider tenantProvider)
    {
        _service = service;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PlatformContentItemDto>>>> List(
        [FromQuery] string? slot = null,
        [FromQuery] bool onlyActive = false,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.ListAsync(tenantId, slot, onlyActive, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PlatformContentItemDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] PlatformContentCreateRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var id = await _service.CreateAsync(tenantId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id, ContentId = id }, HttpContext.TraceIdentifier));
    }

    [HttpPatch("{id}")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        string id,
        [FromBody] PlatformContentUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _service.UpdateAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Success = true }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(
        string id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _service.DeleteAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Success = true }, HttpContext.TraceIdentifier));
    }
}
