using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Audit.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("audit")]
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
    [Authorize]
    public ActionResult<ApiResponse<PagedResult<AuditListItem>>> Get([FromQuery] PagedRequest request)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = _auditQueryService.QueryAudits(request, tenantId);
        var payload = ApiResponse<PagedResult<AuditListItem>>.Ok(result, HttpContext.TraceIdentifier);
        return Ok(payload);
    }
}