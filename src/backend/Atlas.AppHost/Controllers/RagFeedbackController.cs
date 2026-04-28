using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/v1/rag-feedback")]
[Authorize]
public sealed class RagFeedbackController : ControllerBase
{
    private readonly IRagFeedbackService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public RagFeedbackController(
        IRagFeedbackService service,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] RagFeedbackCreateRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, "Unauthorized.", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var id = await _service.CreateAsync(tenantId, currentUser.UserId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RagFeedbackDto>>>> Query(
        [FromQuery] string queryId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var rows = await _service.GetByQueryIdAsync(tenantId, queryId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<RagFeedbackDto>>.Ok(rows, HttpContext.TraceIdentifier));
    }
}
