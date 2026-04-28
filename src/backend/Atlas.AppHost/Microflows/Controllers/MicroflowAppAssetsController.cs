using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Exceptions;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Microflows.Controllers;

[Route("api/v1/microflow-apps")]
public sealed class MicroflowAppAssetsController : MicroflowApiControllerBase
{
    private readonly IMicroflowAppAssetService _appAssetService;
    private readonly IMicroflowRequestContextAccessor _requestContextAccessor;

    public MicroflowAppAssetsController(
        IMicroflowAppAssetService appAssetService,
        IMicroflowRequestContextAccessor requestContextAccessor)
        : base(requestContextAccessor)
    {
        _appAssetService = appAssetService;
        _requestContextAccessor = requestContextAccessor;
    }

    [HttpGet("{appId}")]
    [ProducesResponseType(typeof(MicroflowApiResponse<MicroflowAppAssetDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<MicroflowAppAssetDto>>> GetApp(
        string appId,
        [FromQuery] string? workspaceId,
        CancellationToken cancellationToken)
    {
        var result = await _appAssetService.GetAppAsync(appId, ResolveWorkspaceId(workspaceId), cancellationToken);
        return MicroflowOk(result);
    }

    [HttpGet("{appId}/modules")]
    [ProducesResponseType(typeof(MicroflowApiResponse<IReadOnlyList<MicroflowModuleAssetDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<IReadOnlyList<MicroflowModuleAssetDto>>>> ListModules(
        string appId,
        [FromQuery] string? workspaceId,
        CancellationToken cancellationToken)
    {
        var result = await _appAssetService.ListModulesAsync(appId, ResolveWorkspaceId(workspaceId), cancellationToken);
        return MicroflowOk(result);
    }

    private string ResolveWorkspaceId(string? workspaceId)
    {
        var resolved = workspaceId ?? _requestContextAccessor.Current.WorkspaceId;
        if (string.IsNullOrWhiteSpace(resolved))
        {
            throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowWorkspaceForbidden, "缺少工作区上下文，无法加载微流应用资产。", StatusCodes.Status403Forbidden);
        }

        return resolved;
    }
}
