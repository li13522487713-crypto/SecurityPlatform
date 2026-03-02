using AutoMapper;
using FluentValidation;
using Atlas.Application.Abstractions;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Audit.Models;
using Atlas.Application.Models;
using Atlas.Application.Options;
using Atlas.Core.Exceptions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Helpers;
using Atlas.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthTokenService _authTokenService;
    private readonly IAuthProfileService _authProfileService;
    private readonly IUserCommandService _userCommandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IClientContextAccessor _clientContextAccessor;
    private readonly ICaptchaService _captchaService;
    private readonly IMapper _mapper;
    private readonly IValidator<AuthTokenRequest> _validator;
    private readonly IValidator<AuthRefreshRequest> _refreshValidator;
    private readonly IValidator<ChangePasswordViewModel> _changePasswordValidator;
    private readonly IAuditRecorder _auditRecorder;
    private readonly SecurityOptions _securityOptions;

    public AuthController(
        IAuthTokenService authTokenService,
        IAuthProfileService authProfileService,
        IUserCommandService userCommandService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IClientContextAccessor clientContextAccessor,
        ICaptchaService captchaService,
        IMapper mapper,
        IValidator<AuthTokenRequest> validator,
        IValidator<AuthRefreshRequest> refreshValidator,
        IValidator<ChangePasswordViewModel> changePasswordValidator,
        IAuditRecorder auditRecorder,
        IOptions<SecurityOptions> securityOptions)
    {
        _authTokenService = authTokenService;
        _authProfileService = authProfileService;
        _userCommandService = userCommandService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _clientContextAccessor = clientContextAccessor;
        _captchaService = captchaService;
        _mapper = mapper;
        _validator = validator;
        _refreshValidator = refreshValidator;
        _changePasswordValidator = changePasswordValidator;
        _auditRecorder = auditRecorder;
        _securityOptions = securityOptions.Value;
    }

    [HttpGet("captcha")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public ActionResult<ApiResponse<object>> GetCaptcha()
    {
        var (key, image) = _captchaService.Generate();
        return Ok(ApiResponse<object>.Ok(new { captchaKey = key, captchaImage = image }, HttpContext.TraceIdentifier));
    }

    [HttpPost("token")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<ApiResponse<AuthTokenResult>>> CreateToken(
        [FromBody] AuthTokenViewModel request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        if (tenantId.IsEmpty)
        {
            throw new BusinessException("缺少租户标识", ErrorCodes.ValidationError);
        }

        var dto = _mapper.Map<AuthTokenRequest>(request);
        _validator.ValidateAndThrow(dto);

        // 若前端已提供验证码（风控触发后），在此处校验
        if (!string.IsNullOrWhiteSpace(dto.CaptchaKey))
        {
            if (string.IsNullOrWhiteSpace(dto.CaptchaCode)
                || !_captchaService.Validate(dto.CaptchaKey, dto.CaptchaCode))
            {
                throw new BusinessException("验证码错误或已过期", ErrorCodes.ValidationError);
            }
        }

        var context = new AuthRequestContext(
            ControllerHelper.GetIpAddress(HttpContext),
            ControllerHelper.GetUserAgent(HttpContext),
            ControllerHelper.GetClientContext(HttpContext));
        var result = await _authTokenService.CreateTokenAsync(dto, tenantId, context, cancellationToken);

        // 设置 httpOnly cookie 存储令牌（安全加固）
        SetAuthCookies(result.AccessToken, result.RefreshToken, result.ExpiresAt, result.RefreshExpiresAt);

        var payload = ApiResponse<AuthTokenResult>.Ok(result, HttpContext.TraceIdentifier);
        return Ok(payload);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<ApiResponse<AuthTokenResult>>> RefreshToken(
        [FromBody] AuthRefreshViewModel request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        if (tenantId.IsEmpty)
        {
            throw new BusinessException("缺少租户标识", ErrorCodes.ValidationError);
        }

        var dto = _mapper.Map<AuthRefreshRequest>(request);
        _refreshValidator.ValidateAndThrow(dto);

        var context = new AuthRequestContext(
            ControllerHelper.GetIpAddress(HttpContext),
            ControllerHelper.GetUserAgent(HttpContext),
            ControllerHelper.GetClientContext(HttpContext));
        var result = await _authTokenService.RefreshTokenAsync(dto, tenantId, context, cancellationToken);

        // 刷新令牌时也更新cookie
        SetAuthCookies(result.AccessToken, result.RefreshToken, result.ExpiresAt, result.RefreshExpiresAt);

        return Ok(ApiResponse<AuthTokenResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<AuthProfileResult>>> Me(CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var profile = await _authProfileService.GetProfileAsync(
            currentUser.UserId,
            currentUser.TenantId,
            cancellationToken);
        if (profile is null)
        {
            return NotFound(ApiResponse<AuthProfileResult>.Fail(ErrorCodes.NotFound, "用户不存在", HttpContext.TraceIdentifier));
        }

        var clientContext = _clientContextAccessor.GetCurrent();
        var payloadProfile = profile with
        {
            ClientContext = new ClientContextView(
                clientContext.ClientType.ToString(),
                clientContext.ClientPlatform.ToString(),
                clientContext.ClientChannel.ToString(),
                clientContext.ClientAgent.ToString())
        };
        return Ok(ApiResponse<AuthProfileResult>.Ok(payloadProfile, HttpContext.TraceIdentifier));
    }

    [HttpPut("password")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> ChangePassword(
        [FromBody] ChangePasswordViewModel request,
        CancellationToken cancellationToken)
    {
        _changePasswordValidator.ValidateAndThrow(request);
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var tenantId = _tenantProvider.GetTenantId();
        var dto = _mapper.Map<ChangePasswordRequest>(request);

        await _userCommandService.ChangePasswordAsync(
            tenantId,
            currentUser.UserId,
            dto.CurrentPassword,
            dto.NewPassword,
            cancellationToken);

        var actor = string.IsNullOrWhiteSpace(currentUser.Username) ? currentUser.UserId.ToString() : currentUser.Username;
        var auditContext = new AuditContext(
            currentUser.TenantId,
            actor,
            "CHANGE_PASSWORD",
            "SUCCESS",
            null,
            ControllerHelper.GetIpAddress(HttpContext),
            ControllerHelper.GetUserAgent(HttpContext),
            _clientContextAccessor.GetCurrent());
        await _auditRecorder.RecordAsync(auditContext, cancellationToken);

        return Ok(ApiResponse<object>.Ok(new { Success = true }, HttpContext.TraceIdentifier));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> Logout(CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        if (currentUser.SessionId > 0)
        {
            var context = new AuthRequestContext(
                ControllerHelper.GetIpAddress(HttpContext),
                ControllerHelper.GetUserAgent(HttpContext),
                ControllerHelper.GetClientContext(HttpContext));
            await _authTokenService.RevokeSessionAsync(
                currentUser.UserId,
                currentUser.TenantId,
                currentUser.SessionId,
                context,
                cancellationToken);
        }

        var actor = string.IsNullOrWhiteSpace(currentUser.Username) ? currentUser.UserId.ToString() : currentUser.Username;
        var auditContext = new AuditContext(
            currentUser.TenantId,
            actor,
            "LOGOUT",
            "SUCCESS",
            null,
            ControllerHelper.GetIpAddress(HttpContext),
            ControllerHelper.GetUserAgent(HttpContext),
            _clientContextAccessor.GetCurrent());

        await _auditRecorder.RecordAsync(auditContext, cancellationToken);

        // 清除认证cookie
        ClearAuthCookies();

        return Ok(ApiResponse<object>.Ok(new { Success = true }, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 设置认证相关的httpOnly cookie
    /// </summary>
    private void SetAuthCookies(string accessToken, string refreshToken, DateTimeOffset accessExpires, DateTimeOffset refreshExpires)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true, // 生产环境强制HTTPS
            SameSite = SameSiteMode.Strict,
            Path = "/"
        };

        // 设置 Access Token Cookie
        var accessCookieOptions = new CookieOptions
        {
            HttpOnly = cookieOptions.HttpOnly,
            Secure = cookieOptions.Secure,
            SameSite = cookieOptions.SameSite,
            Path = cookieOptions.Path,
            Expires = accessExpires
        };
        HttpContext.Response.Cookies.Append("access_token", accessToken, accessCookieOptions);

        // 设置 Refresh Token Cookie
        var refreshCookieOptions = new CookieOptions
        {
            HttpOnly = cookieOptions.HttpOnly,
            Secure = cookieOptions.Secure,
            SameSite = cookieOptions.SameSite,
            Path = cookieOptions.Path,
            Expires = refreshExpires
        };
        HttpContext.Response.Cookies.Append("refresh_token", refreshToken, refreshCookieOptions);
    }

    /// <summary>
    /// 清除认证cookie
    /// </summary>
    private void ClearAuthCookies()
    {
        HttpContext.Response.Cookies.Delete("access_token", new CookieOptions
        {
            Path = "/",
            Secure = true,
            SameSite = SameSiteMode.Strict
        });
        HttpContext.Response.Cookies.Delete("refresh_token", new CookieOptions
        {
            Path = "/",
            Secure = true,
            SameSite = SameSiteMode.Strict
        });
    }
}

