using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/runtime/plugins")]
[Authorize]
public sealed class RuntimePluginsController : ControllerBase
{
    private readonly ILowCodePluginService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUser;

    public RuntimePluginsController(ILowCodePluginService service, ITenantProvider tenantProvider, ICurrentUserAccessor currentUser)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUser = currentUser;
    }

    [HttpPost("{pluginId}:invoke")]
    public async Task<ActionResult<ApiResponse<PluginInvokeResult>>> Invoke(string pluginId, [FromBody] PluginInvokeRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        var r = await _service.InvokeAsync(tenantId, user.UserId, request with { PluginId = pluginId }, cancellationToken);
        return Ok(ApiResponse<PluginInvokeResult>.Ok(r, HttpContext.TraceIdentifier));
    }
}
