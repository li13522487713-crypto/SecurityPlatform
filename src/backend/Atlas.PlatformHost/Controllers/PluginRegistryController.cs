using Atlas.Core.Models;
using Atlas.Core.Plugins;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.PlatformHost.Controllers;

[ApiController]
[Route("api/v1/logic-flow/plugins")]
[Authorize(Policy = PermissionPolicies.SystemAdmin)]
public sealed class PluginRegistryController : ControllerBase
{
    private readonly IPluginRegistry _registry;

    public PluginRegistryController(IPluginRegistry registry)
    {
        _registry = registry;
    }

    [HttpGet]
    public ActionResult<ApiResponse<IReadOnlyList<PluginInfo>>> GetAll()
    {
        var list = _registry.GetAll();
        return Ok(ApiResponse<IReadOnlyList<PluginInfo>>.Ok(list, HttpContext.TraceIdentifier));
    }

    [HttpGet("nodes")]
    public ActionResult<ApiResponse<IReadOnlyList<PluginInfo>>> GetNodes()
    {
        var list = _registry.GetAll().Where(x => x.PluginType == "Node").ToList();
        return Ok(ApiResponse<IReadOnlyList<PluginInfo>>.Ok(list, HttpContext.TraceIdentifier));
    }

    [HttpGet("functions")]
    public ActionResult<ApiResponse<IReadOnlyList<PluginInfo>>> GetFunctions()
    {
        var list = _registry.GetAll().Where(x => x.PluginType == "Function").ToList();
        return Ok(ApiResponse<IReadOnlyList<PluginInfo>>.Ok(list, HttpContext.TraceIdentifier));
    }

    [HttpGet("data-sources")]
    public ActionResult<ApiResponse<IReadOnlyList<PluginInfo>>> GetDataSources()
    {
        var list = _registry.GetAll().Where(x => x.PluginType == "DataSource").ToList();
        return Ok(ApiResponse<IReadOnlyList<PluginInfo>>.Ok(list, HttpContext.TraceIdentifier));
    }

    [HttpGet("templates")]
    public ActionResult<ApiResponse<IReadOnlyList<PluginInfo>>> GetTemplates()
    {
        var list = _registry.GetAll().Where(x => x.PluginType == "Template").ToList();
        return Ok(ApiResponse<IReadOnlyList<PluginInfo>>.Ok(list, HttpContext.TraceIdentifier));
    }
}
