using Atlas.Application.AiPlatform.Abstractions.Channels;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers.Channels;

/// <summary>
/// 治理 M-G02-C11：微信公众号渠道凭据管理。
/// </summary>
[ApiController]
[Route("api/v1/workspaces/{workspaceId}/publish-channels/{channelId}/wechat-mp-credential")]
[Authorize]
public sealed class WechatMpChannelCredentialsController : ControllerBase
{
    private readonly IWechatMpChannelCredentialService _service;
    private readonly ITenantProvider _tenantProvider;

    public WechatMpChannelCredentialsController(
        IWechatMpChannelCredentialService service,
        ITenantProvider tenantProvider)
    {
        _service = service;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceView)]
    public async Task<ActionResult<ApiResponse<WechatMpChannelCredentialDto?>>> Get(
        string workspaceId,
        string channelId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var dto = await _service.GetAsync(tenantId, workspaceId, channelId, cancellationToken);
        return Ok(ApiResponse<WechatMpChannelCredentialDto?>.Ok(dto, HttpContext.TraceIdentifier));
    }

    [HttpPut]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceUpdate)]
    public async Task<ActionResult<ApiResponse<WechatMpChannelCredentialDto>>> Upsert(
        string workspaceId,
        string channelId,
        [FromBody] WechatMpChannelCredentialUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var dto = await _service.UpsertAsync(tenantId, workspaceId, channelId, request, cancellationToken);
        return Ok(ApiResponse<WechatMpChannelCredentialDto>.Ok(dto, HttpContext.TraceIdentifier));
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
