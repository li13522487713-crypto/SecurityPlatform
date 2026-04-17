using Atlas.Application.Coze.Abstractions;
using Atlas.Application.Coze.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers;

/// <summary>
/// Coze PRD 个人设置（PRD 03 头像入口）。所有接口隐式作用于当前登录用户，
/// 客户端不传 userId（防越权）。
/// </summary>
[ApiController]
[Route("api/v1/me")]
[Authorize]
public sealed class MeSettingsController : ControllerBase
{
    private readonly IMeSettingsService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public MeSettingsController(
        IMeSettingsService service,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet("settings/general")]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceView)]
    public async Task<ActionResult<ApiResponse<MeGeneralSettingsDto>>> GetGeneral(
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var result = await _service.GetGeneralAsync(tenantId, currentUser, cancellationToken);
        return Ok(ApiResponse<MeGeneralSettingsDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPatch("settings/general")]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceView)]
    public async Task<ActionResult<ApiResponse<MeGeneralSettingsDto>>> UpdateGeneral(
        [FromBody] MeGeneralSettingsUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var result = await _service.UpdateGeneralAsync(tenantId, currentUser, request, cancellationToken);
        return Ok(ApiResponse<MeGeneralSettingsDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("settings/publish-channels")]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MePublishChannelDto>>>> ListPublishChannels(
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var result = await _service.ListPublishChannelsAsync(tenantId, currentUser, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<MePublishChannelDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("settings/datasources")]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MeDataSourceDto>>>> ListDataSources(
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var result = await _service.ListDataSourcesAsync(tenantId, currentUser, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<MeDataSourceDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpDelete("account")]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceView)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteAccount(CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        await _service.DeleteAccountAsync(tenantId, currentUser, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Success = true }, HttpContext.TraceIdentifier));
    }
}
