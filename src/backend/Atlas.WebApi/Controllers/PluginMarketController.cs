using Atlas.Application.Plugins.Abstractions;
using Atlas.Core.Models;
using Atlas.Core.Plugins;
using Atlas.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

/// <summary>
/// 插件市场 API
/// </summary>
[ApiController]
[Route("api/v1/plugin-market")]
[Authorize]
public sealed class PluginMarketController : ControllerBase
{
    private readonly IPluginMarketQueryService _queryService;
    private readonly IPluginMarketCommandService _commandService;

    public PluginMarketController(
        IPluginMarketQueryService queryService,
        IPluginMarketCommandService commandService)
    {
        _queryService = queryService;
        _commandService = commandService;
    }

    /// <summary>分页搜索插件市场（支持 keyword/category 筛选）</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> Search(
        [FromQuery] PagedRequest request,
        [FromQuery] string? keyword = null,
        [FromQuery] PluginCategory? category = null,
        CancellationToken cancellationToken = default)
    {
        var (items, total) = await _queryService.SearchAsync(keyword, category, request.PageIndex, request.PageSize, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new
        {
            request.PageIndex,
            request.PageSize,
            Total = total,
            Items = items
        }, HttpContext.TraceIdentifier));
    }

    /// <summary>获取插件详情</summary>
    [HttpGet("{code}")]
    public async Task<ActionResult<ApiResponse<object>>> GetByCode(
        string code,
        CancellationToken cancellationToken = default)
    {
        var entry = await _queryService.GetByCodeAsync(code, cancellationToken);
        if (entry is null)
        {
            return NotFound(ApiResponse<object>.Fail("NOT_FOUND", "插件不存在", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<object>.Ok(entry, HttpContext.TraceIdentifier));
    }

    /// <summary>获取插件版本历史</summary>
    [HttpGet("{code}/versions")]
    public async Task<ActionResult<ApiResponse<object>>> GetVersions(
        string code,
        CancellationToken cancellationToken = default)
    {
        var entry = await _queryService.GetByCodeAsync(code, cancellationToken);
        if (entry is null)
        {
            return NotFound(ApiResponse<object>.Fail("NOT_FOUND", "插件不存在", HttpContext.TraceIdentifier));
        }

        var versions = await _queryService.GetVersionsAsync(entry.Id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(versions, HttpContext.TraceIdentifier));
    }

    /// <summary>发布/更新插件到市场</summary>
    [HttpPost]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Publish(
        [FromBody] PublishPluginMarketRequest request,
        CancellationToken cancellationToken = default)
    {
        // 从 JWT claim 获取租户 ID
        var tenantIdStr = User.FindFirst("tid")?.Value ?? User.FindFirst("tenantId")?.Value;
        Guid.TryParse(tenantIdStr, out var tenantId);

        var id = await _commandService.PublishAsync(request, tenantId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id }, HttpContext.TraceIdentifier));
    }

    /// <summary>更新插件市场信息</summary>
    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] UpdatePluginMarketRequest request,
        CancellationToken cancellationToken = default)
    {
        await _commandService.UpdateAsync(id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    /// <summary>弃用插件</summary>
    [HttpPost("{id:long}/deprecate")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Deprecate(
        long id,
        CancellationToken cancellationToken = default)
    {
        await _commandService.DeprecateAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }
}
