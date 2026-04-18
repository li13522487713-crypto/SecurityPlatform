using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

/// <summary>
/// 运行时发布产物只读端点（M17 S17-1）：/api/runtime/publish/{appId}/artifacts
/// </summary>
[ApiController]
[Route("api/runtime/publish")]
[Authorize]
public sealed class RuntimePublishArtifactsController : ControllerBase
{
    private readonly IAppPublishService _service;
    private readonly ITenantProvider _tenantProvider;

    public RuntimePublishArtifactsController(IAppPublishService service, ITenantProvider tenantProvider)
    {
        _service = service;
        _tenantProvider = tenantProvider;
    }

    [HttpGet("{appId:long}/artifacts")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PublishArtifactDto>>>> List(long appId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var list = await _service.ListAsync(tenantId, appId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PublishArtifactDto>>.Ok(list, HttpContext.TraceIdentifier));
    }
}
