using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Exceptions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/v1/project-ide/apps/{appId:long}")]
[Authorize]
public sealed class ProjectIdeController : ControllerBase
{
    private readonly IProjectIdeBootstrapService _bootstrapService;
    private readonly IProjectIdeDependencyGraphService _dependencyGraphService;
    private readonly IProjectIdePublishOrchestrator _publishOrchestrator;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUser;

    public ProjectIdeController(
        IProjectIdeBootstrapService bootstrapService,
        IProjectIdeDependencyGraphService dependencyGraphService,
        IProjectIdePublishOrchestrator publishOrchestrator,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUser)
    {
        _bootstrapService = bootstrapService;
        _dependencyGraphService = dependencyGraphService;
        _publishOrchestrator = publishOrchestrator;
        _tenantProvider = tenantProvider;
        _currentUser = currentUser;
    }

    [HttpGet("bootstrap")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppView)]
    public async Task<ActionResult<ApiResponse<ProjectIdeBootstrapDto>>> GetBootstrap(long appId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var bootstrap = await _bootstrapService.GetBootstrapAsync(tenantId, appId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"应用不存在：{appId}");
        return Ok(ApiResponse<ProjectIdeBootstrapDto>.Ok(bootstrap, HttpContext.TraceIdentifier));
    }

    [HttpGet("graph")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppView)]
    public async Task<ActionResult<ApiResponse<ProjectIdeGraphDto>>> GetGraph(long appId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var graph = await _dependencyGraphService.GetGraphAsync(tenantId, appId, schemaJsonOverride: null, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"应用不存在：{appId}");
        return Ok(ApiResponse<ProjectIdeGraphDto>.Ok(graph, HttpContext.TraceIdentifier));
    }

    [HttpPost("validate")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppView)]
    public async Task<ActionResult<ApiResponse<ProjectIdeValidationResultDto>>> Validate(
        long appId,
        [FromBody] ProjectIdeValidationRequest? request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _bootstrapService.ValidateAsync(tenantId, appId, request?.SchemaJson, cancellationToken);
        return Ok(ApiResponse<ProjectIdeValidationResultDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("publish")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppPublish)]
    public async Task<ActionResult<ApiResponse<ProjectIdePublishResultDto>>> Publish(
        long appId,
        [FromBody] ProjectIdePublishRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        var result = await _publishOrchestrator.PublishAsync(tenantId, user.UserId, appId, request, cancellationToken);
        return Ok(ApiResponse<ProjectIdePublishResultDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("publish-preview")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppView)]
    public async Task<ActionResult<ApiResponse<ProjectIdePublishPreviewDto>>> GetPublishPreview(long appId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _bootstrapService.GetPublishPreviewAsync(tenantId, appId, schemaJsonOverride: null, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"应用不存在：{appId}");
        return Ok(ApiResponse<ProjectIdePublishPreviewDto>.Ok(result, HttpContext.TraceIdentifier));
    }
}
