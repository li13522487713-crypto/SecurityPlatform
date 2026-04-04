using Atlas.Application.Monitor.Abstractions;
using Atlas.Application.Monitor.Models;
using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

/// <summary>
/// 服务监控（等保2.0：运维操作需要权限控制）
/// </summary>
[ApiController]
[Route("api/v1/monitor")]
[Authorize]
public sealed class MonitorController : ControllerBase
{
    private readonly IServerInfoQueryService _serverInfoQueryService;
    private readonly IComplianceEvidencePackageService _complianceEvidencePackageService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ITenantAppInstanceQueryService _tenantAppInstanceQueryService;

    public MonitorController(
        IServerInfoQueryService serverInfoQueryService,
        IComplianceEvidencePackageService complianceEvidencePackageService,
        ITenantProvider tenantProvider,
        ITenantAppInstanceQueryService tenantAppInstanceQueryService)
    {
        _serverInfoQueryService = serverInfoQueryService;
        _complianceEvidencePackageService = complianceEvidencePackageService;
        _tenantProvider = tenantProvider;
        _tenantAppInstanceQueryService = tenantAppInstanceQueryService;
    }

    /// <summary>获取服务器当前状态快照</summary>
    [HttpGet("server-info")]
    [Authorize(Policy = PermissionPolicies.MonitorView)]
    public async Task<ActionResult<ApiResponse<ServerInfoDto>>> GetServerInfo(
        CancellationToken cancellationToken = default)
    {
        var info = await _serverInfoQueryService.GetServerInfoAsync(cancellationToken);
        return Ok(ApiResponse<ServerInfoDto>.Ok(info, HttpContext.TraceIdentifier));
    }

    /// <summary>导出等保证据包（zip）</summary>
    [HttpGet("compliance/evidence-package")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<IActionResult> ExportComplianceEvidencePackage(CancellationToken cancellationToken = default)
    {
        var package = await _complianceEvidencePackageService.BuildPackageAsync(cancellationToken);
        return File(package.Content, package.ContentType, package.FileName);
    }

    /// <summary>查询租户应用实例运行态观测快照</summary>
    [HttpGet("app-runtime-hosts")]
    [Authorize(Policy = PermissionPolicies.MonitorView)]
    public async Task<ActionResult<ApiResponse<PagedResult<TenantAppInstanceListItem>>>> GetAppRuntimeHosts(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        if (tenantId.IsEmpty)
        {
            return BadRequest(ApiResponse<PagedResult<TenantAppInstanceListItem>>.Fail(
                ErrorCodes.ValidationError,
                "TenantIdRequired",
                HttpContext.TraceIdentifier));
        }

        var result = await _tenantAppInstanceQueryService.QueryAsync(tenantId, request, cancellationToken);
        return Ok(ApiResponse<PagedResult<TenantAppInstanceListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }
}
