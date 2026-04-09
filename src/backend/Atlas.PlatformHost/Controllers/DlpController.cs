using Atlas.Application.Governance.Abstractions;
using Atlas.Application.Governance.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Atlas.Presentation.Shared.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers;

[ApiController]
[Route("api/v1/dlp")]
[Authorize]
public sealed class DlpController : ControllerBase
{
    private readonly IDlpService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public DlpController(
        IDlpService service,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet("classifications")]
    [Authorize(Policy = PermissionPolicies.ToolPoliciesView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DataClassificationResponse>>>> GetClassifications(CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var rows = await _service.GetClassificationsAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DataClassificationResponse>>.Ok(rows, HttpContext.TraceIdentifier));
    }

    [HttpPost("classifications")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> CreateClassification(
        [FromBody] DataClassificationRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "Unauthorized"), HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var id = await _service.CreateClassificationAsync(tenantId, currentUser.UserId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id }, HttpContext.TraceIdentifier));
    }

    [HttpGet("labels")]
    [Authorize(Policy = PermissionPolicies.ToolPoliciesView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SensitiveLabelResponse>>>> GetLabels(CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var rows = await _service.GetLabelsAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<SensitiveLabelResponse>>.Ok(rows, HttpContext.TraceIdentifier));
    }

    [HttpPost("labels")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> CreateLabel(
        [FromBody] SensitiveLabelRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "Unauthorized"), HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var id = await _service.CreateLabelAsync(tenantId, currentUser.UserId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id }, HttpContext.TraceIdentifier));
    }

    [HttpGet("policies")]
    [Authorize(Policy = PermissionPolicies.ToolPoliciesView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DlpPolicyResponse>>>> GetPolicies(CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var rows = await _service.GetPoliciesAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DlpPolicyResponse>>.Ok(rows, HttpContext.TraceIdentifier));
    }

    [HttpPost("policies")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> CreatePolicy(
        [FromBody] DlpPolicyRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "Unauthorized"), HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var id = await _service.CreatePolicyAsync(tenantId, currentUser.UserId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id }, HttpContext.TraceIdentifier));
    }

    [HttpGet("outbound-channels")]
    [Authorize(Policy = PermissionPolicies.ToolPoliciesView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<OutboundChannelResponse>>>> GetOutboundChannels(CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var rows = await _service.GetOutboundChannelsAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<OutboundChannelResponse>>.Ok(rows, HttpContext.TraceIdentifier));
    }

    [HttpPost("outbound-channels")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> CreateOutboundChannel(
        [FromBody] OutboundChannelRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "Unauthorized"), HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var id = await _service.CreateOutboundChannelAsync(tenantId, currentUser.UserId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id }, HttpContext.TraceIdentifier));
    }

    [HttpPost("bindings")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> BindMaskPolicy(
        [FromBody] DlpBindingRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "Unauthorized"), HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.BindMaskPolicyAsync(tenantId, currentUser.UserId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("export-jobs")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> CreateExportJob(
        [FromBody] DlpTransferJobRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "Unauthorized"), HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.CreateExportJobAsync(tenantId, currentUser.UserId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("download-jobs")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> CreateDownloadJob(
        [FromBody] DlpTransferJobRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "Unauthorized"), HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.CreateDownloadJobAsync(tenantId, currentUser.UserId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("external-share-approvals")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> CreateExternalShareApproval(
        [FromBody] ExternalShareApprovalRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "Unauthorized"), HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.CreateExternalShareApprovalAsync(tenantId, currentUser.UserId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("outbound-checks")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<OutboundCheckResponse>>> CheckOutbound(
        [FromBody] OutboundCheckRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<OutboundCheckResponse>.Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "Unauthorized"), HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.CheckOutboundAsync(tenantId, currentUser.UserId, request, cancellationToken);
        return Ok(ApiResponse<OutboundCheckResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("events")]
    [Authorize(Policy = PermissionPolicies.ToolPoliciesView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LeakageEventResponse>>>> GetEvents(CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var rows = await _service.GetEventsAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<LeakageEventResponse>>.Ok(rows, HttpContext.TraceIdentifier));
    }

    [HttpGet("evidence-packages")]
    [Authorize(Policy = PermissionPolicies.ToolPoliciesView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<EvidencePackageResponse>>>> GetEvidencePackages(CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var rows = await _service.GetEvidencePackagesAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<EvidencePackageResponse>>.Ok(rows, HttpContext.TraceIdentifier));
    }
}
