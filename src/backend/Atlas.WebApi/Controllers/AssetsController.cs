using Atlas.Application.Assets.Abstractions;
using Atlas.Application.Assets.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("assets")]
public sealed class AssetsController : ControllerBase
{
    private readonly IAssetQueryService _assetQueryService;
    private readonly ITenantProvider _tenantProvider;

    public AssetsController(IAssetQueryService assetQueryService, ITenantProvider tenantProvider)
    {
        _assetQueryService = assetQueryService;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    [Authorize]
    public ActionResult<ApiResponse<PagedResult<AssetListItem>>> Get([FromQuery] PagedRequest request)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = _assetQueryService.QueryAssets(request, tenantId);
        var payload = ApiResponse<PagedResult<AssetListItem>>.Ok(result, HttpContext.TraceIdentifier);
        return Ok(payload);
    }
}