using Atlas.Application.AiPlatform.Models;
using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

/// <summary>
/// 应用资源聚合（设计态 v1 /api/v1/lowcode/apps/{appId}/resources）。
/// AppHost 侧镜像，供 app-web 在 direct 模式下调用。
/// </summary>
[ApiController]
[Route("api/v1/lowcode/apps/{appId:long}/resources")]
public sealed class LowCodeAppResourcesController : ControllerBase
{
    private readonly IAppResourceCatalogService _service;
    private readonly ILowCodeAppResourceBindingService _bindingService;
    private readonly ITenantProvider _tenantProvider;

    public LowCodeAppResourcesController(
        IAppResourceCatalogService service,
        ILowCodeAppResourceBindingService bindingService,
        ITenantProvider tenantProvider)
    {
        _service = service;
        _bindingService = bindingService;
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
        [FromQuery] bool boundOnly = false,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var catalog = await _service.SearchAsync(tenantId, new AppResourceQuery(types, keyword, pageIndex, pageSize), cancellationToken);

        if (!boundOnly)
        {
            return Ok(ApiResponse<AppResourceCatalogDto>.Ok(catalog, HttpContext.TraceIdentifier));
        }

        var bindings = await _bindingService.ListByAppAsync(tenantId, appId, resourceType: null, cancellationToken);
        var byTypeKeys = bindings
            .GroupBy(binding => binding.ResourceType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(binding => binding.ResourceId.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

        var filtered = new Dictionary<string, IReadOnlyList<AppResourceItem>>(StringComparer.OrdinalIgnoreCase);
        var total = 0;
        foreach (var (type, items) in catalog.ByType)
        {
            if (!byTypeKeys.TryGetValue(type, out var allowed) || allowed.Count == 0)
            {
                continue;
            }

            var kept = items.Where(item => allowed.Contains(item.Id)).ToList();
            filtered[type] = kept;
            total += kept.Count;
        }

        return Ok(ApiResponse<AppResourceCatalogDto>.Ok(new AppResourceCatalogDto(filtered, total), HttpContext.TraceIdentifier));
    }

    [HttpGet("bindings")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AiAppResourceBindingDto>>>> ListBindings(
        long appId,
        [FromQuery] string? resourceType,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _bindingService.ListByAppAsync(tenantId, appId, resourceType, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AiAppResourceBindingDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("bindings")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Bind(
        long appId,
        [FromBody] AiAppResourceBindingCreateRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var id = await _bindingService.BindAsync(tenantId, appId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("bindings/{bindingId:long}")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateBinding(
        long appId,
        long bindingId,
        [FromBody] AiAppResourceBindingUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _bindingService.UpdateAsync(tenantId, appId, bindingId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = bindingId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("bindings/{resourceType}/{resourceId:long}")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Unbind(
        long appId,
        string resourceType,
        long resourceId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _bindingService.UnbindAsync(tenantId, appId, resourceType, resourceId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = resourceId.ToString() }, HttpContext.TraceIdentifier));
    }
}
