using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Audit.Models;
using Atlas.Application.Identity;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Audit.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Authorization;
using Atlas.Presentation.Shared.Models;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.PlatformHost.Controllers;

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

    [HttpGet("by-resource")]
    [Authorize(Policy = PermissionPolicies.AuditView)]
    public async Task<ActionResult<ApiResponse<PagedResult<AuditListItem>>>> GetByResource(
        [FromQuery] PagedRequest request,
        [FromQuery] string? actorId,
        [FromQuery] string? action,
        [FromQuery] string? resourceId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var queryResult = await _auditQueryService.QueryAuditsByResourceAsync(
            request, tenantId, actorId, action, resourceId, from, to, cancellationToken);
        return Ok(ApiResponse<PagedResult<AuditListItem>>.Ok(queryResult, HttpContext.TraceIdentifier));
    }

    [HttpGet("export/csv")]
    [Authorize(Policy = PermissionPolicies.AuditView)]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] string? action,
        [FromQuery] string? result,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int maxRows = 10000,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var items = await _auditQueryService.ExportAuditsCsvAsync(
            tenantId, action, result, from, to, maxRows, cancellationToken);

        var csv = BuildCsv(items);
        var fileName = $"audit-export-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        return File(System.Text.Encoding.UTF8.GetPreamble().Concat(
                System.Text.Encoding.UTF8.GetBytes(csv)).ToArray(),
            "text/csv; charset=utf-8",
            fileName);
    }

    private static string BuildCsv(IReadOnlyList<AuditListItem> items)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("操作人,操作类型,操作结果,操作目标,IP地址,客户端类型,发生时间");
        foreach (var item in items)
        {
            sb.AppendLine(string.Join(",",
                EscapeCsvField(item.Actor),
                EscapeCsvField(item.Action),
                EscapeCsvField(item.Result),
                EscapeCsvField(item.Target),
                EscapeCsvField(item.IpAddress ?? string.Empty),
                EscapeCsvField(item.ClientType ?? string.Empty),
                EscapeCsvField(item.OccurredAt.ToString("yyyy-MM-dd HH:mm:ss"))));
        }
        return sb.ToString();
    }

    private static string EscapeCsvField(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
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

    [HttpGet("last-client-error")]
    [Authorize(Policy = PermissionPolicies.AuditView)]
    public async Task<ActionResult<string>> GetLastClientError(CancellationToken cancellationToken)
    {
        var result = await _auditQueryService.QueryAuditsAsync(new PagedRequest(1, 10, null, null, true), _tenantProvider.GetTenantId(), "CLIENT_ERROR", null, cancellationToken);
        var last = result.Items.FirstOrDefault();
        if (last == null) return "No error found";
        return Ok(last.Target);
    }
}

