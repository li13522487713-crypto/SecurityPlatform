using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Audit.Models;
using Atlas.Application.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.WebApi.Authorization;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/audit")]
public sealed class AuditController : ControllerBase
{
    private readonly IAuditQueryService _auditQueryService;
    private readonly ITenantProvider _tenantProvider;

    public AuditController(IAuditQueryService auditQueryService, ITenantProvider tenantProvider)
    {
        _auditQueryService = auditQueryService;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AuditView)]
    public async Task<ActionResult<ApiResponse<PagedResult<AuditListItem>>>> Get(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _auditQueryService.QueryAuditsAsync(request, tenantId, cancellationToken);
        var payload = ApiResponse<PagedResult<AuditListItem>>.Ok(result, HttpContext.TraceIdentifier);
        return Ok(payload);
    }
}

