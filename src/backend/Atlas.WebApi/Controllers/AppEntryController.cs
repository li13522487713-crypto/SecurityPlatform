using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.WebApi.Filters;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/platform/app-entry")]
[PlatformOnly]
public sealed class AppEntryController : ControllerBase
{
    private readonly IAppEntryQueryService appEntryQueryService;
    private readonly ITenantProvider tenantProvider;

    public AppEntryController(
        IAppEntryQueryService appEntryQueryService,
        ITenantProvider tenantProvider)
    {
        this.appEntryQueryService = appEntryQueryService;
        this.tenantProvider = tenantProvider;
    }

    [HttpGet("{appKey}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AppEntryInfo>>> GetEntry(
        string appKey,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantProvider.GetTenantId();
        if (tenantId.IsEmpty)
        {
            return BadRequest(ApiResponse<AppEntryInfo>.Fail(ErrorCodes.ValidationError, "TenantIdRequired", HttpContext.TraceIdentifier));
        }

        var entry = await appEntryQueryService.GetEntryAsync(tenantId, appKey, cancellationToken);
        if (entry is null)
        {
            return NotFound(ApiResponse<AppEntryInfo>.Fail(ErrorCodes.NotFound, "App entry not found.", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<AppEntryInfo>.Ok(entry, HttpContext.TraceIdentifier));
    }

    [HttpPost("{appKey}/begin-login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AppEntryLoginBeginResult>>> BeginLogin(
        string appKey,
        [FromBody] AppEntryLoginBeginRequest? request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantProvider.GetTenantId();
        if (tenantId.IsEmpty)
        {
            return BadRequest(ApiResponse<AppEntryLoginBeginResult>.Fail(ErrorCodes.ValidationError, "TenantIdRequired", HttpContext.TraceIdentifier));
        }

        var beginResult = await appEntryQueryService.BeginLoginAsync(
            tenantId,
            appKey,
            request?.RedirectUri,
            cancellationToken);
        if (beginResult is null)
        {
            return NotFound(ApiResponse<AppEntryLoginBeginResult>.Fail(ErrorCodes.NotFound, "App entry not found.", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<AppEntryLoginBeginResult>.Ok(beginResult, HttpContext.TraceIdentifier));
    }

    [HttpGet("{appKey}/login-options")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AppEntryLoginOptions>>> GetLoginOptions(
        string appKey,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantProvider.GetTenantId();
        if (tenantId.IsEmpty)
        {
            return BadRequest(ApiResponse<AppEntryLoginOptions>.Fail(ErrorCodes.ValidationError, "TenantIdRequired", HttpContext.TraceIdentifier));
        }

        var loginOptions = await appEntryQueryService.GetLoginOptionsAsync(tenantId, appKey, cancellationToken);
        if (loginOptions is null)
        {
            return NotFound(ApiResponse<AppEntryLoginOptions>.Fail(ErrorCodes.NotFound, "App entry not found.", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<AppEntryLoginOptions>.Ok(loginOptions, HttpContext.TraceIdentifier));
    }
}
