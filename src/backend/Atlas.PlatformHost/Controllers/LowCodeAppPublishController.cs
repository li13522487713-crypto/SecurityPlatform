using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers;

/// <summary>
/// 应用发布控制器（M17 S17-1，**设计态 v1** /api/v1/lowcode/apps/{id}/publish）。
/// </summary>
[ApiController]
[Route("api/v1/lowcode/apps/{appId:long}")]
public sealed class LowCodeAppPublishController : ControllerBase
{
    private readonly IAppPublishService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUser;

    public LowCodeAppPublishController(IAppPublishService service, ITenantProvider tenantProvider, ICurrentUserAccessor currentUser)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUser = currentUser;
    }

    [HttpPost("publish/{kind}")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppPublish)]
    public async Task<ActionResult<ApiResponse<PublishArtifactDto>>> Publish(long appId, string kind, [FromBody] PublishRequest? request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        var req = (request ?? new PublishRequest(kind, null, null)) with { Kind = kind };
        var r = await _service.PublishAsync(tenantId, user.UserId, appId, req, cancellationToken);
        return Ok(ApiResponse<PublishArtifactDto>.Ok(r, HttpContext.TraceIdentifier));
    }

    [HttpGet("artifacts")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PublishArtifactDto>>>> List(long appId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var list = await _service.ListAsync(tenantId, appId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PublishArtifactDto>>.Ok(list, HttpContext.TraceIdentifier));
    }

    [HttpPost("publish/rollback")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppPublish)]
    public async Task<ActionResult<ApiResponse<object>>> Rollback(long appId, [FromBody] PublishRollbackRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        await _service.RevokeAsync(tenantId, user.UserId, appId, request.ArtifactId, "rollback", cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }
}

// runtime 端点（GET /api/runtime/publish/{appId}/artifacts）放在 Atlas.AppHost.Controllers.RuntimePublishArtifactsController。
