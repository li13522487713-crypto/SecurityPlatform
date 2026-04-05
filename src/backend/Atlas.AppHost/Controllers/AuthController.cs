using System.Security.Claims;
using Atlas.Application.Abstractions;
using Atlas.Application.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly ICurrentUserAccessor currentUserAccessor;
    private readonly IClientContextAccessor clientContextAccessor;
    private readonly IAuthTokenService authTokenService;
    private readonly ITenantProvider tenantProvider;

    public AuthController(
        ICurrentUserAccessor currentUserAccessor,
        IClientContextAccessor clientContextAccessor,
        IAuthTokenService authTokenService,
        ITenantProvider tenantProvider)
    {
        this.currentUserAccessor = currentUserAccessor;
        this.clientContextAccessor = clientContextAccessor;
        this.authTokenService = authTokenService;
        this.tenantProvider = tenantProvider;
    }

    [HttpPost("token")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthTokenResult>>> Token(
        [FromBody] AuthTokenRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantProvider.GetTenantId();
        var context = new AuthRequestContext(
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            HttpContext.Request.Headers.UserAgent.ToString(),
            clientContextAccessor.GetCurrent());
        var result = await authTokenService.CreateTokenAsync(request, tenantId, context, cancellationToken);
        return Ok(ApiResponse<AuthTokenResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthTokenResult>>> Refresh(
        [FromBody] AuthRefreshRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantProvider.GetTenantId();
        var context = new AuthRequestContext(
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            HttpContext.Request.Headers.UserAgent.ToString(),
            clientContextAccessor.GetCurrent());
        var result = await authTokenService.RefreshTokenAsync(request, tenantId, context, cancellationToken);
        return Ok(ApiResponse<AuthTokenResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("me")]
    [Authorize(Policy = PermissionPolicies.AppUser)]
    public ActionResult<ApiResponse<object>> Me()
    {
        var user = currentUserAccessor.GetCurrentUserOrThrow();
        var roles = User.Claims
            .Where(claim => claim.Type == ClaimTypes.Role)
            .Select(claim => claim.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var permissions = User.Claims
            .Where(claim => claim.Type is "permission" or "perm")
            .Select(claim => claim.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Ok(ApiResponse<object>.Ok(new
        {
            userId = user.UserId,
            username = user.Username,
            tenantId = user.TenantId.ToString(),
            sessionId = user.SessionId,
            roles,
            permissions
        }, HttpContext.TraceIdentifier));
    }

    [HttpPost("logout")]
    [Authorize(Policy = PermissionPolicies.AppUser)]
    public async Task<ActionResult<ApiResponse<object>>> Logout(CancellationToken cancellationToken)
    {
        var user = currentUserAccessor.GetCurrentUserOrThrow();
        if (user.SessionId > 0)
        {
            var context = new AuthRequestContext(
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                HttpContext.Request.Headers.UserAgent.ToString(),
                clientContextAccessor.GetCurrent());
            await authTokenService.RevokeSessionAsync(user.UserId, user.TenantId, user.SessionId, context, cancellationToken);
        }

        return Ok(ApiResponse<object>.Ok(new { success = true }, HttpContext.TraceIdentifier));
    }
}
