using Atlas.Application.Plugins.Abstractions;
using Atlas.Application.Plugins.Models;
using Atlas.Core.Models;
using Atlas.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/plugins")]
public sealed class PluginsController : ControllerBase
{
    private readonly IPluginCatalogService _pluginCatalogService;

    public PluginsController(IPluginCatalogService pluginCatalogService)
    {
        _pluginCatalogService = pluginCatalogService;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PluginDescriptor>>>> Get(CancellationToken cancellationToken)
    {
        var plugins = await _pluginCatalogService.GetPluginsAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PluginDescriptor>>.Ok(plugins, HttpContext.TraceIdentifier));
    }

    [HttpPost("reload")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Reload(CancellationToken cancellationToken)
    {
        await _pluginCatalogService.ReloadAsync(cancellationToken);
        var plugins = await _pluginCatalogService.GetPluginsAsync(cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Count = plugins.Count }, HttpContext.TraceIdentifier));
    }
}
