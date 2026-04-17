using Atlas.Application.Coze.Abstractions;
using Atlas.Application.Coze.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers;

/// <summary>
/// Coze PRD 模板/插件商店“分类摘要”（PRD 02-7.7、7.8）。
/// 完整模板/插件搜索仍走现有 <c>TemplatesController</c> / <c>AiMarketplaceController</c>。
/// </summary>
[ApiController]
[Route("api/v1/market")]
[Authorize]
public sealed class MarketSummaryController : ControllerBase
{
    private readonly IMarketSummaryService _service;
    private readonly ITenantProvider _tenantProvider;

    public MarketSummaryController(IMarketSummaryService service, ITenantProvider tenantProvider)
    {
        _service = service;
        _tenantProvider = tenantProvider;
    }

    [HttpGet("templates/summary")]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceView)]
    public async Task<ActionResult<ApiResponse<PagedResult<MarketCategorySummaryDto>>>> ListTemplateCategories(
        [FromQuery] string? keyword = null,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var paged = new PagedRequest { PageIndex = pageIndex, PageSize = pageSize, Keyword = keyword };
        var result = await _service.ListTemplateCategoriesAsync(tenantId, keyword, paged, cancellationToken);
        return Ok(ApiResponse<PagedResult<MarketCategorySummaryDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("plugins/summary")]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceView)]
    public async Task<ActionResult<ApiResponse<PagedResult<MarketCategorySummaryDto>>>> ListPluginCategories(
        [FromQuery] string? keyword = null,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var paged = new PagedRequest { PageIndex = pageIndex, PageSize = pageSize, Keyword = keyword };
        var result = await _service.ListPluginCategoriesAsync(tenantId, keyword, paged, cancellationToken);
        return Ok(ApiResponse<PagedResult<MarketCategorySummaryDto>>.Ok(result, HttpContext.TraceIdentifier));
    }
}
