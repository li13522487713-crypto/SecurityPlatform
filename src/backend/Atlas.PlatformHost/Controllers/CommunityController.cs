using Atlas.Application.Coze.Abstractions;
using Atlas.Application.Coze.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers;

/// <summary>
/// Coze PRD 作品社区（PRD 02-7.9）。
/// </summary>
[ApiController]
[Route("api/v1/community")]
[Authorize]
public sealed class CommunityController : ControllerBase
{
    private readonly ICommunityService _service;
    private readonly ITenantProvider _tenantProvider;

    public CommunityController(ICommunityService service, ITenantProvider tenantProvider)
    {
        _service = service;
        _tenantProvider = tenantProvider;
    }

    [HttpGet("works")]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceView)]
    public async Task<ActionResult<ApiResponse<PagedResult<CommunityWorkItemDto>>>> ListWorks(
        [FromQuery] string? keyword = null,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var paged = new PagedRequest
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Keyword = keyword
        };
        var result = await _service.ListWorksAsync(tenantId, keyword, paged, cancellationToken);
        return Ok(ApiResponse<PagedResult<CommunityWorkItemDto>>.Ok(result, HttpContext.TraceIdentifier));
    }
}
