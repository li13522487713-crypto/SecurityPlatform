using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers;

/// <summary>
/// 应用资源聚合（M07 S07-3，**设计态 v1** /api/v1/lowcode/apps/{id}/resources）。
/// </summary>
[ApiController]
[Route("api/v1/lowcode/apps/{appId:long}/resources")]
public sealed class LowCodeAppResourcesController : ControllerBase
{
    private readonly IAppResourceCatalogService _service;
    private readonly ITenantProvider _tenantProvider;

    public LowCodeAppResourcesController(IAppResourceCatalogService service, ITenantProvider tenantProvider)
    {
        _service = service;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.LowcodeAppView)]
    public async Task<ActionResult<ApiResponse<AppResourceCatalogDto>>> Search(
        long appId,
        [FromQuery] string? types,
        [FromQuery] string? keyword,
        [FromQuery] int? pageIndex,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        // appId 当前未参与查询（资源在租户级），但仍接受路径参数以满足 PLAN.md S07-3 路由设计；
        // 后续 M14 资源引用反查能基于 appId 二次过滤。
        _ = appId;
        var tenantId = _tenantProvider.GetTenantId();
        var r = await _service.SearchAsync(tenantId, new AppResourceQuery(types, keyword, pageIndex, pageSize), cancellationToken);
        return Ok(ApiResponse<AppResourceCatalogDto>.Ok(r, HttpContext.TraceIdentifier));
    }
}
