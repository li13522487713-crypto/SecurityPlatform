using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers;

[ApiController]
[Route("api/v2/appbridge")]
[Authorize]
public sealed class AppBridgeController : ControllerBase
{
    private readonly IAppBridgeQueryService _queryService;
    private readonly IAppBridgeCommandService _commandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public AppBridgeController(
        IAppBridgeQueryService queryService,
        IAppBridgeCommandService commandService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet("online-apps")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<PagedResult<OnlineAppProjectionItem>>>> GetOnlineApps(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.QueryOnlineAppsAsync(tenantId, request, cancellationToken);
        return Ok(ApiResponse<PagedResult<OnlineAppProjectionItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("online-apps/{appInstanceId:long}")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<OnlineAppProjectionDetail>>> GetOnlineAppById(
        long appInstanceId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetOnlineAppByInstanceIdAsync(tenantId, appInstanceId, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<OnlineAppProjectionDetail>.Fail(ErrorCodes.NotFound, "Online app not found.", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<OnlineAppProjectionDetail>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("apps/{appInstanceId:long}/exposure-policy")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<AppExposurePolicyResponse>>> GetExposurePolicy(
        long appInstanceId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetExposurePolicyAsync(tenantId, appInstanceId, cancellationToken);
        return Ok(ApiResponse<AppExposurePolicyResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPut("apps/{appInstanceId:long}/exposure-policy")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<AppExposurePolicyResponse>>> UpdateExposurePolicy(
        long appInstanceId,
        [FromBody] AppExposurePolicyUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<AppExposurePolicyResponse>.Fail(ErrorCodes.Unauthorized, "Unauthorized.", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _commandService.UpdateExposurePolicyAsync(tenantId, currentUser.UserId, appInstanceId, request, cancellationToken);
        return Ok(ApiResponse<AppExposurePolicyResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("commands")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> CreateCommand(
        [FromBody] AppCommandCreateRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, "Unauthorized.", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var idempotencyKey = HttpContext.Request.Headers["Idempotency-Key"].ToString();
        var commandId = await _commandService.CreateCommandAsync(tenantId, currentUser.UserId, request, idempotencyKey, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = commandId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpGet("commands")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<PagedResult<AppCommandListItem>>>> QueryCommands(
        [FromQuery] PagedRequest request,
        [FromQuery] string? appInstanceId,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.QueryCommandsAsync(tenantId, request, appInstanceId, status, cancellationToken);
        return Ok(ApiResponse<PagedResult<AppCommandListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("commands/{commandId:long}")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<AppCommandDetail>>> GetCommand(
        long commandId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetCommandByIdAsync(tenantId, commandId, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<AppCommandDetail>.Fail(ErrorCodes.NotFound, "Command not found.", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<AppCommandDetail>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("apps/{appInstanceId:long}/exposed-data/query")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<ExposedDataQueryResponse>>> QueryExposedData(
        long appInstanceId,
        [FromBody] ExposedDataQueryRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.QueryExposedDataAsync(tenantId, appInstanceId, request, cancellationToken);
        return Ok(ApiResponse<ExposedDataQueryResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("federated/register")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> RegisterFederated(
        [FromBody] FederatedRegisterRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.RegisterFederatedAsync(tenantId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Registered = true }, HttpContext.TraceIdentifier));
    }

    [HttpPost("federated/heartbeat")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> FederatedHeartbeat(
        [FromBody] FederatedHeartbeatRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.HeartbeatFederatedAsync(tenantId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Heartbeat = true }, HttpContext.TraceIdentifier));
    }

    [HttpGet("federated/commands/pending")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AppCommandDetail>>>> GetPendingFederatedCommands(
        [FromQuery] string appInstanceId,
        CancellationToken cancellationToken)
    {
        if (!long.TryParse(appInstanceId, out var appInstanceIdValue))
        {
            return BadRequest(ApiResponse<IReadOnlyList<AppCommandDetail>>.Fail(ErrorCodes.ValidationError, "appInstanceId invalid.", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var rows = await _queryService.GetPendingFederatedCommandsAsync(tenantId, appInstanceIdValue, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AppCommandDetail>>.Ok(rows, HttpContext.TraceIdentifier));
    }

    [HttpPost("federated/commands/{commandId:long}/ack")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> AckFederatedCommand(
        long commandId,
        [FromBody] FederatedCommandAckRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.AcknowledgeFederatedCommandAsync(tenantId, commandId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Acked = true }, HttpContext.TraceIdentifier));
    }

    [HttpPost("federated/commands/{commandId:long}/result")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> ReportFederatedCommandResult(
        long commandId,
        [FromBody] FederatedCommandResultRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.CompleteFederatedCommandAsync(tenantId, commandId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Completed = true }, HttpContext.TraceIdentifier));
    }
}
