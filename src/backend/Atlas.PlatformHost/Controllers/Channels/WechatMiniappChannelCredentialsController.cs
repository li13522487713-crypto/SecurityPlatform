using Atlas.Application.AiPlatform.Abstractions.Channels;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers.Channels;

[ApiController]
[Route("api/v1/workspaces/{workspaceId}/publish-channels/{channelId}/wechat-miniapp-credential")]
[Authorize]
public sealed class WechatMiniappChannelCredentialsController : ControllerBase
{
    private readonly IWechatMiniappChannelCredentialService _service;
    private readonly ITenantProvider _tenantProvider;

    public WechatMiniappChannelCredentialsController(
        IWechatMiniappChannelCredentialService service,
        ITenantProvider tenantProvider)
    {
        _service = service;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceView)]
    public async Task<ActionResult<ApiResponse<WechatMiniappChannelCredentialDto?>>> Get(
        string workspaceId,
        string channelId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var dto = await _service.GetAsync(tenantId, workspaceId, channelId, cancellationToken);
        return Ok(ApiResponse<WechatMiniappChannelCredentialDto?>.Ok(dto, HttpContext.TraceIdentifier));
    }

    [HttpPut]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceUpdate)]
    public async Task<ActionResult<ApiResponse<WechatMiniappChannelCredentialDto>>> Upsert(
        string workspaceId,
        string channelId,
        [FromBody] WechatMiniappChannelCredentialUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var dto = await _service.UpsertAsync(tenantId, workspaceId, channelId, request, cancellationToken);
        return Ok(ApiResponse<WechatMiniappChannelCredentialDto>.Ok(dto, HttpContext.TraceIdentifier));
    }

    [HttpDelete]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(
        string workspaceId,
        string channelId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _service.DeleteAsync(tenantId, workspaceId, channelId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Success = true }, HttpContext.TraceIdentifier));
    }
}
