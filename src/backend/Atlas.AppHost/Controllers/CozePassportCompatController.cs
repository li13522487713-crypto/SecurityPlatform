using Atlas.Application.Abstractions;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

[ApiController]
public sealed class CozePassportCompatController : ControllerBase
{
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IClientContextAccessor _clientContextAccessor;
    private readonly IAuthTokenService _authTokenService;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IUserCommandService _userCommandService;
    private readonly ITenantProvider _tenantProvider;

    public CozePassportCompatController(
        ICurrentUserAccessor currentUserAccessor,
        IClientContextAccessor clientContextAccessor,
        IAuthTokenService authTokenService,
        IUserAccountRepository userAccountRepository,
        IUserCommandService userCommandService,
        ITenantProvider tenantProvider)
    {
        _currentUserAccessor = currentUserAccessor;
        _clientContextAccessor = clientContextAccessor;
        _authTokenService = authTokenService;
        _userAccountRepository = userAccountRepository;
        _userCommandService = userCommandService;
        _tenantProvider = tenantProvider;
    }

    [HttpPost("/api/passport/account/info/v2/")]
    [Authorize(Policy = PermissionPolicies.AppUser)]
    public async Task<ActionResult<object>> PassportAccountInfo(CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var tenantId = _tenantProvider.GetTenantId();
        var account = await _userAccountRepository.FindByIdAsync(tenantId, currentUser.UserId, cancellationToken);
        if (account is null)
        {
            return Unauthorized(Fail("user not found"));
        }

        return Ok(Success(BuildPassportUser(account)));
    }

    [HttpGet("/api/passport/web/logout/")]
    [Authorize(Policy = PermissionPolicies.AppUser)]
    public async Task<ActionResult<object>> PassportLogout(
        [FromQuery] string? next,
        CancellationToken cancellationToken)
    {
        var user = _currentUserAccessor.GetCurrentUserOrThrow();
        if (user.SessionId > 0)
        {
            var context = new AuthRequestContext(
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                HttpContext.Request.Headers.UserAgent.ToString(),
                _clientContextAccessor.GetCurrent());
            await _authTokenService.RevokeSessionAsync(user.UserId, user.TenantId, user.SessionId, context, cancellationToken);
        }

        return Ok(new
        {
            code = 0,
            msg = "success",
            redirect_url = NormalizeRedirect(next) ?? "/"
        });
    }

    [HttpPost("/api/web/user/update/upload_avatar/")]
    [Authorize(Policy = PermissionPolicies.AppUser)]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public ActionResult<object> UploadAvatar([FromForm] IFormFile? avatar)
    {
        _ = avatar;
        return Ok(Success(new
        {
            web_uri = string.Empty
        }));
    }

    [HttpPost("/api/user/update_profile")]
    [Authorize(Policy = PermissionPolicies.AppUser)]
    public async Task<ActionResult<object>> UpdateProfile(
        [FromBody] CozePassportUpdateProfileRequest? request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var tenantId = _tenantProvider.GetTenantId();
        var account = await _userAccountRepository.FindByIdAsync(tenantId, currentUser.UserId, cancellationToken);
        if (account is null)
        {
            return Unauthorized(Fail("user not found"));
        }

        var displayName = string.IsNullOrWhiteSpace(request?.name)
            ? (string.IsNullOrWhiteSpace(account.DisplayName) ? account.Username : account.DisplayName)
            : request!.name!.Trim();

        await _userCommandService.UpdateProfileAsync(
            tenantId,
            currentUser.UserId,
            displayName,
            string.IsNullOrWhiteSpace(account.Email) ? null : account.Email,
            string.IsNullOrWhiteSpace(account.PhoneNumber) ? null : account.PhoneNumber,
            cancellationToken);

        return Ok(new
        {
            code = 0,
            msg = "success"
        });
    }

    private static object BuildPassportUser(UserAccount account)
    {
        return new
        {
            user_id_str = account.Id.ToString(),
            name = string.IsNullOrWhiteSpace(account.DisplayName) ? account.Username : account.DisplayName,
            user_unique_name = account.Username,
            email = account.Email ?? string.Empty,
            description = string.Empty,
            avatar_url = string.Empty,
            screen_name = string.IsNullOrWhiteSpace(account.DisplayName) ? account.Username : account.DisplayName,
            app_user_info = new
            {
                user_unique_name = account.Username
            },
            locale = "zh-CN",
            user_create_time = 0
        };
    }

    private static object Success(object data)
    {
        return new
        {
            code = 0,
            msg = "success",
            data
        };
    }

    private static object Fail(string message)
    {
        return new
        {
            code = 401,
            msg = message
        };
    }

    private static string? NormalizeRedirect(string? redirect)
    {
        if (string.IsNullOrWhiteSpace(redirect))
        {
            return null;
        }

        var trimmed = redirect.Trim();
        return trimmed.StartsWith("/", StringComparison.Ordinal) ? trimmed : null;
    }
}

public sealed record CozePassportUpdateProfileRequest(
    string? name,
    string? user_unique_name,
    string? description,
    string? locale);
