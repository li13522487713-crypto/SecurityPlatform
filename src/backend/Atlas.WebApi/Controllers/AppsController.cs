using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Core.Identity;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.WebApi.Authorization;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/apps")]
public sealed class AppsController : ControllerBase
{
    private readonly IAppConfigQueryService _queryService;
    private readonly IAppConfigCommandService _commandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly IValidator<AppConfigUpdateRequest> _updateValidator;
    private readonly IAppContextAccessor _appContextAccessor;

    public AppsController(
        IAppConfigQueryService queryService,
        IAppConfigCommandService commandService,
        ITenantProvider tenantProvider,
        IValidator<AppConfigUpdateRequest> updateValidator,
        IAppContextAccessor appContextAccessor)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tenantProvider = tenantProvider;
        _updateValidator = updateValidator;
        _appContextAccessor = appContextAccessor;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<PagedResult<AppConfigListItem>>>> Get(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.QueryAsync(request, tenantId, cancellationToken);
        return Ok(ApiResponse<PagedResult<AppConfigListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("current")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<AppConfigDetail>>> GetCurrent(CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var appId = _appContextAccessor.GetAppId();
        var detail = await _queryService.GetByAppIdAsync(appId, tenantId, cancellationToken);
        if (detail is null)
        {
            return NotFound(ApiResponse<AppConfigDetail>.Fail(ErrorCodes.NotFound, "App not found.", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<AppConfigDetail>.Ok(detail, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<AppConfigDetail>>> GetById(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var detail = await _queryService.GetDetailAsync(id, tenantId, cancellationToken);
        if (detail is null)
        {
            return NotFound(ApiResponse<AppConfigDetail>.Fail(ErrorCodes.NotFound, "App not found.", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<AppConfigDetail>.Ok(detail, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] AppConfigUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _updateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdateAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }
}
