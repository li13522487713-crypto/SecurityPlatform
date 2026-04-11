using Atlas.Application.Abstractions;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Atlas.Presentation.Shared.Models;
using FluentValidation;
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
    private readonly IUserCommandService userCommandService;
    private readonly IUserAccountRepository userAccountRepository;
    private readonly IAuthProfileService authProfileService;
    private readonly IValidator<ChangePasswordViewModel> changePasswordValidator;
    private readonly IValidator<UserProfileUpdateViewModel> profileUpdateValidator;
    private readonly ITenantProvider tenantProvider;

    public AuthController(
        ICurrentUserAccessor currentUserAccessor,
        IClientContextAccessor clientContextAccessor,
        IAuthTokenService authTokenService,
        IUserCommandService userCommandService,
        IUserAccountRepository userAccountRepository,
        IAuthProfileService authProfileService,
        IValidator<ChangePasswordViewModel> changePasswordValidator,
        IValidator<UserProfileUpdateViewModel> profileUpdateValidator,
        ITenantProvider tenantProvider)
    {
        this.currentUserAccessor = currentUserAccessor;
        this.clientContextAccessor = clientContextAccessor;
        this.authTokenService = authTokenService;
        this.userCommandService = userCommandService;
        this.userAccountRepository = userAccountRepository;
        this.authProfileService = authProfileService;
        this.changePasswordValidator = changePasswordValidator;
        this.profileUpdateValidator = profileUpdateValidator;
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
    public async Task<ActionResult<ApiResponse<AuthProfileResult>>> Me(CancellationToken cancellationToken)
    {
        var user = currentUserAccessor.GetCurrentUserOrThrow();
        var profile = await authProfileService.GetProfileAsync(user.UserId, user.TenantId, cancellationToken);
        if (profile is null)
        {
            return NotFound(ApiResponse<AuthProfileResult>.Fail(ErrorCodes.NotFound, "User not found.", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<AuthProfileResult>.Ok(profile, HttpContext.TraceIdentifier));
    }

    [HttpGet("profile")]
    [Authorize(Policy = PermissionPolicies.AppUser)]
    public async Task<ActionResult<ApiResponse<UserProfileDetailViewModel>>> GetProfile(CancellationToken cancellationToken)
    {
        var currentUser = currentUserAccessor.GetCurrentUserOrThrow();
        var user = await userAccountRepository.FindByIdAsync(currentUser.TenantId, currentUser.UserId, cancellationToken);
        if (user is null)
        {
            return NotFound(ApiResponse<UserProfileDetailViewModel>.Fail(ErrorCodes.NotFound, "User not found.", HttpContext.TraceIdentifier));
        }

        var profile = new UserProfileDetailViewModel(
            user.DisplayName,
            user.Email,
            user.PhoneNumber);
        return Ok(ApiResponse<UserProfileDetailViewModel>.Ok(profile, HttpContext.TraceIdentifier));
    }

    [HttpPut("profile")]
    [Authorize(Policy = PermissionPolicies.AppUser)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateProfile(
        [FromBody] UserProfileUpdateViewModel request,
        CancellationToken cancellationToken)
    {
        profileUpdateValidator.ValidateAndThrow(request);
        var currentUser = currentUserAccessor.GetCurrentUserOrThrow();
        var tenantId = tenantProvider.GetTenantId();

        await userCommandService.UpdateProfileAsync(
            tenantId,
            currentUser.UserId,
            request.DisplayName.Trim(),
            string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            cancellationToken);

        return Ok(ApiResponse<object>.Ok(new { success = true }, HttpContext.TraceIdentifier));
    }

    [HttpPut("password")]
    [Authorize(Policy = PermissionPolicies.AppUser)]
    public async Task<ActionResult<ApiResponse<object>>> ChangePassword(
        [FromBody] ChangePasswordViewModel request,
        CancellationToken cancellationToken)
    {
        changePasswordValidator.ValidateAndThrow(request);
        var currentUser = currentUserAccessor.GetCurrentUserOrThrow();
        var tenantId = tenantProvider.GetTenantId();

        await userCommandService.ChangePasswordAsync(
            tenantId,
            currentUser.UserId,
            request.CurrentPassword,
            request.NewPassword,
            cancellationToken);

        return Ok(ApiResponse<object>.Ok(new { success = true }, HttpContext.TraceIdentifier));
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
