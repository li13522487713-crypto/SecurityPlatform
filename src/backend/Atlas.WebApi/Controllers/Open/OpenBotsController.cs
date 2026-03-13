using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers.Open;

[ApiController]
[Route("api/v1/open/bots")]
[Authorize(AuthenticationSchemes = PatAuthenticationHandler.SchemeName)]
public sealed class OpenBotsController : ControllerBase
{
    private readonly IAgentQueryService _queryService;
    private readonly ITenantProvider _tenantProvider;

    public OpenBotsController(IAgentQueryService queryService, ITenantProvider tenantProvider)
    {
        _queryService = queryService;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<AgentListItem>>>> GetPaged(
        [FromQuery] PagedRequest request,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        if (!OpenScopeHelper.HasScope(User, "open:bots:read"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<PagedResult<AgentListItem>>.Fail(
                ErrorCodes.Forbidden,
                "PAT 缺少 open:bots:read 权限",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetPagedAsync(
            tenantId,
            keyword,
            status: null,
            request.PageIndex,
            request.PageSize,
            cancellationToken);
        return Ok(ApiResponse<PagedResult<AgentListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResponse<AgentDetail>>> GetById(long id, CancellationToken cancellationToken)
    {
        if (!OpenScopeHelper.HasScope(User, "open:bots:read"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<AgentDetail>.Fail(
                ErrorCodes.Forbidden,
                "PAT 缺少 open:bots:read 权限",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetByIdAsync(tenantId, id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<AgentDetail>.Fail(ErrorCodes.NotFound, "Bot 不存在", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<AgentDetail>.Ok(result, HttpContext.TraceIdentifier));
    }
}
