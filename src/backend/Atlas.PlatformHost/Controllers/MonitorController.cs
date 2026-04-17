using Atlas.Application.Monitor.Abstractions;
using Atlas.Application.Monitor.Models;
using Atlas.Core.Models;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.PlatformHost.Controllers;

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

    public MonitorController(
        IServerInfoQueryService serverInfoQueryService,
        IComplianceEvidencePackageService complianceEvidencePackageService)
    {
        _serverInfoQueryService = serverInfoQueryService;
        _complianceEvidencePackageService = complianceEvidencePackageService;
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
}
