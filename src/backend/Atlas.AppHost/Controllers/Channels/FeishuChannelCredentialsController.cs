using Atlas.Application.AiPlatform.Abstractions.Channels;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers.Channels;

[ApiController]
[Route("api/v1/workspaces/{workspaceId}/publish-channels/{channelId}/feishu-credential")]
[Authorize]
public sealed class FeishuChannelCredentialsController : ControllerBase
{
    private readonly IFeishuChannelCredentialService _service;
    private readonly ITenantProvider _tenantProvider;

    public FeishuChannelCredentialsController(
        IFeishuChannelCredentialService service,
        ITenantProvider tenantProvider)
    {
        _service = service;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceView)]
    public async Task<ActionResult<ApiResponse<FeishuChannelCredentialDto?>>> Get(
        string workspaceId,
        string channelId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var dto = await _service.GetAsync(tenantId, workspaceId, channelId, cancellationToken);
        return Ok(ApiResponse<FeishuChannelCredentialDto?>.Ok(dto, HttpContext.TraceIdentifier));
    }

    [HttpPut]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceUpdate)]
    public async Task<ActionResult<ApiResponse<FeishuChannelCredentialDto>>> Upsert(
        string workspaceId,
        string channelId,
        [FromBody] FeishuChannelCredentialUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var dto = await _service.UpsertAsync(tenantId, workspaceId, channelId, request, cancellationToken);
        return Ok(ApiResponse<FeishuChannelCredentialDto>.Ok(dto, HttpContext.TraceIdentifier));
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
