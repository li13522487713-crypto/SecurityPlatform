using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers;

[ApiController]
[Route("api/v1/lowcode/plugins")]
public sealed class LowCodePluginsController : ControllerBase
{
    private readonly ILowCodePluginService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUser;

    public LowCodePluginsController(ILowCodePluginService service, ITenantProvider tenantProvider, ICurrentUserAccessor currentUser)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUser = currentUser;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.LowcodeAppView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PluginDefinitionDto>>>> Search([FromQuery] string? keyword, [FromQuery] string? shareScope, [FromQuery] int? pageIndex, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var list = await _service.SearchDefsAsync(tenantId, keyword, shareScope, pageIndex ?? 1, pageSize ?? 20, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PluginDefinitionDto>>.Ok(list, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.LowcodeAppUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Upsert([FromBody] PluginUpsertRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        var id = await _service.UpsertDefAsync(tenantId, user.UserId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        await _service.DeleteDefAsync(tenantId, user.UserId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/publish")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppPublish)]
    public async Task<ActionResult<ApiResponse<object>>> PublishVersion(long id, [FromBody] PluginPublishVersionRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        var versionId = await _service.PublishVersionAsync(tenantId, user.UserId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { versionId = versionId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{pluginId}/authorize")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Authorize(string pluginId, [FromBody] PluginAuthorizeRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        var id = await _service.AuthorizeAsync(tenantId, user.UserId, pluginId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpGet("{pluginId}/usage")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppView)]
    public async Task<ActionResult<ApiResponse<PluginUsageDto?>>> Usage(string pluginId, [FromQuery] string day, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var u = await _service.GetUsageAsync(tenantId, pluginId, day, cancellationToken);
        return Ok(ApiResponse<PluginUsageDto?>.Ok(u, HttpContext.TraceIdentifier));
    }
}
