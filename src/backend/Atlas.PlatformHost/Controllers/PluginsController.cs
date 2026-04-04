using Atlas.Application.Plugins.Abstractions;
using Atlas.Application.Plugins.Models;
using Atlas.Core.Models;
using Atlas.Domain.Plugins;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Plugins;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.PlatformHost.Controllers;

[ApiController]
[Route("api/v1/plugins")]
public sealed class PluginsController : ControllerBase
{
    private readonly IPluginCatalogService _pluginCatalogService;
    private readonly IPluginConfigService _pluginConfigService;
    private readonly PluginPackageService _packageService;
    private readonly PluginCatalogOptions _pluginOptions;
    private readonly PluginMetricsStore _metricsStore;

    public PluginsController(
        IPluginCatalogService pluginCatalogService,
        IPluginConfigService pluginConfigService,
        PluginPackageService packageService,
        IOptions<PluginCatalogOptions> pluginOptions,
        PluginMetricsStore metricsStore)
    {
        _pluginCatalogService = pluginCatalogService;
        _metricsStore = metricsStore;
        _pluginConfigService = pluginConfigService;
        _packageService = packageService;
        _pluginOptions = pluginOptions.Value;
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

    /// <summary>获取插件合并配置（按 Global/Tenant/App 优先级合并）</summary>
    [HttpGet("{code}/config")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> GetConfig(
        string code,
        [FromQuery] string? tenantId = null,
        [FromQuery] string? appId = null,
        CancellationToken cancellationToken = default)
    {
        var merged = await _pluginConfigService.GetMergedConfigAsync(code, tenantId, appId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { ConfigJson = merged }, HttpContext.TraceIdentifier));
    }

    /// <summary>保存插件配置（指定 Scope）</summary>
    [HttpPut("{code}/config")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> SaveConfig(
        string code,
        [FromBody] SavePluginConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        await _pluginConfigService.SaveConfigAsync(code, request.Scope, request.ScopeId, request.ConfigJson, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    /// <summary>启用插件</summary>
    [HttpPost("{code}/enable")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Enable(string code, CancellationToken cancellationToken = default)
    {
        await _pluginCatalogService.EnableAsync(code, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    /// <summary>禁用插件</summary>
    [HttpPost("{code}/disable")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Disable(string code, CancellationToken cancellationToken = default)
    {
        await _pluginCatalogService.DisableAsync(code, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    /// <summary>卸载插件</summary>
    [HttpPost("{code}/unload")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Unload(string code, CancellationToken cancellationToken = default)
    {
        await _pluginCatalogService.UnloadAsync(code, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    /// <summary>上传安装插件包（.atpkg / .zip）</summary>
    [HttpPost("install")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50 MB 上限
    public async Task<ActionResult<ApiResponse<object>>> Install(
        IFormFile package,
        CancellationToken cancellationToken = default)
    {
        if (package is null || package.Length == 0)
        {
            return BadRequest(ApiResponse<object>.Fail("VALIDATION_ERROR", ApiResponseLocalizer.T(HttpContext, "PluginPackageUploadRequired"), HttpContext.TraceIdentifier));
        }

        await using var stream = package.OpenReadStream();
        var manifest = await _packageService.InstallAsync(stream, _pluginOptions.RootPath, cancellationToken);

        return Ok(ApiResponse<object>.Ok(new
        {
            manifest.Code,
            manifest.Name,
            manifest.Version
        }, HttpContext.TraceIdentifier));
    }

    /// <summary>获取所有插件运行时指标快照</summary>
    [HttpGet("metrics")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public ActionResult<ApiResponse<IReadOnlyList<PluginMetricsSnapshot>>> GetMetrics()
    {
        var snapshots = _metricsStore.GetAllSnapshots();
        return Ok(ApiResponse<IReadOnlyList<PluginMetricsSnapshot>>.Ok(snapshots, HttpContext.TraceIdentifier));
    }

    /// <summary>获取指定插件运行时指标快照</summary>
    [HttpGet("{code}/metrics")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public ActionResult<ApiResponse<PluginMetricsSnapshot>> GetPluginMetrics(string code)
    {
        var snapshot = _metricsStore.GetSnapshot(code);
        if (snapshot is null)
            return NotFound(ApiResponse<PluginMetricsSnapshot>.Fail("NOT_FOUND",
                string.Format(ApiResponseLocalizer.T(HttpContext, "PluginMetricsNotFoundFormat"), code),
                HttpContext.TraceIdentifier));
        return Ok(ApiResponse<PluginMetricsSnapshot>.Ok(snapshot, HttpContext.TraceIdentifier));
    }
}

public sealed record SavePluginConfigRequest(
    PluginConfigScope Scope,
    string? ScopeId,
    string ConfigJson);
