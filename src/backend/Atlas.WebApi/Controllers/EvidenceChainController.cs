using System.Text.Json;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.WebApi.Filters;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/evidence-chain")]
[Authorize]
[PlatformOnly]
public sealed class EvidenceChainController : ControllerBase
{
    private readonly EvidenceChainService _service;
    private readonly ITenantProvider _tenantProvider;

    public EvidenceChainController(EvidenceChainService service, ITenantProvider tenantProvider)
    {
        _service = service;
        _tenantProvider = tenantProvider;
    }

    /// <summary>按业务 Key 导出证据链（JSON）</summary>
    [HttpGet("{businessKey}/export")]
    public async Task<IActionResult> Export(string businessKey, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var chain = await _service.BuildAsync(tenantId, businessKey, cancellationToken);

        var json = JsonSerializer.Serialize(chain, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var fileName = $"evidence-{businessKey}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.json";
        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", fileName);
    }

    /// <summary>按业务 Key 查询证据链摘要（JSON 响应，不触发下载）</summary>
    [HttpGet("{businessKey}")]
    public async Task<ActionResult<ApiResponse<EvidenceChain>>> Get(string businessKey, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var chain = await _service.BuildAsync(tenantId, businessKey, cancellationToken);
        return Ok(ApiResponse<EvidenceChain>.Ok(chain, HttpContext.TraceIdentifier));
    }
}
