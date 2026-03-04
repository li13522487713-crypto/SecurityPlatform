using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Audit.Models;
using Atlas.Application.Identity;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Audit.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.WebApi.Authorization;
using Atlas.WebApi.Models;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/audit")]
public sealed class AuditController : ControllerBase
{
    private readonly IAuditQueryService _auditQueryService;
    private readonly IAuditWriter _auditWriter;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public AuditController(
        IAuditQueryService auditQueryService,
        IAuditWriter auditWriter,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _auditQueryService = auditQueryService;
        _auditWriter = auditWriter;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AuditView)]
    public async Task<ActionResult<ApiResponse<PagedResult<AuditListItem>>>> Get(
        [FromQuery] PagedRequest request,
        [FromQuery] string? action,
        [FromQuery] string? result,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var queryResult = await _auditQueryService.QueryAuditsAsync(request, tenantId, action, result, cancellationToken);
        var payload = ApiResponse<PagedResult<AuditListItem>>.Ok(queryResult, HttpContext.TraceIdentifier);
        return Ok(payload);
    }

    [HttpPost("client-errors")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> ReportClientError(
        [FromBody] ClientErrorReportViewModel request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(ApiResponse<object>.Fail(
                ErrorCodes.ValidationError,
                "message不能为空",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUser();
        var actor = currentUser?.Username ?? "anonymous";
        var target = $"{request.Url ?? "-"}|{request.Component ?? "-"}|{request.Level ?? "error"}|{request.Message}";
        var error = new AuditRecord(
            tenantId,
            actor,
            "CLIENT_ERROR",
            "Failure",
            target,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString());
        await _auditWriter.WriteAsync(error, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Success = true }, HttpContext.TraceIdentifier));
    }
}

