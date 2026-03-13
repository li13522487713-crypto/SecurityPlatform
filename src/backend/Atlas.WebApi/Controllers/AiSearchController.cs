using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/ai-search")]
[Authorize]
public sealed class AiSearchController : ControllerBase
{
    private readonly IAiSearchService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IValidator<AiRecentEditCreateRequest> _recentEditCreateValidator;

    public AiSearchController(
        IAiSearchService service,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IValidator<AiRecentEditCreateRequest> recentEditCreateValidator)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _recentEditCreateValidator = recentEditCreateValidator;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AiSearchView)]
    public async Task<ActionResult<ApiResponse<AiSearchResponse>>> Search(
        [FromQuery] string? keyword = null,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var result = await _service.SearchAsync(tenantId, currentUser.UserId, keyword, limit, cancellationToken);
        return Ok(ApiResponse<AiSearchResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("recent")]
    [Authorize(Policy = PermissionPolicies.AiSearchView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AiRecentEditItem>>>> GetRecent(
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var result = await _service.GetRecentEditsAsync(tenantId, currentUser.UserId, limit, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AiRecentEditItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("recent")]
    [Authorize(Policy = PermissionPolicies.AiSearchUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> RecordRecent(
        [FromBody] AiRecentEditCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        _recentEditCreateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var id = await _service.RecordRecentEditAsync(tenantId, currentUser.UserId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("recent/{id:long}")]
    [Authorize(Policy = PermissionPolicies.AiSearchUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteRecent(
        long id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        await _service.DeleteRecentEditAsync(tenantId, currentUser.UserId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }
}
