using AutoMapper;
using FluentValidation;
using Atlas.Application.Abstractions;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Audit.Models;
using Atlas.Application.Models;
using Atlas.Application.Options;
using Atlas.Application.Security;
using Atlas.Application.System.Abstractions;
using Atlas.Application.Identity.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using Atlas.WebApi.Helpers;
using Atlas.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using System.Diagnostics;

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
    private readonly IMenuQueryService _menuQueryService;
    private readonly ISystemConfigQueryService _systemConfigQueryService;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IMapper _mapper;
    private readonly IValidator<AuthTokenRequest> _validator;
    private readonly IValidator<AuthRefreshRequest> _refreshValidator;
    private readonly IValidator<ChangePasswordViewModel> _changePasswordValidator;
    private readonly IValidator<UserProfileUpdateViewModel> _profileUpdateValidator;
    private readonly IValidator<RegisterViewModel> _registerValidator;
    private readonly IAuditRecorder _auditRecorder;
    private readonly IOptionsMonitor<SecurityOptions> _securityOptionsMonitor;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthTokenService authTokenService,
        IAuthProfileService authProfileService,
        IUserCommandService userCommandService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IClientContextAccessor clientContextAccessor,
        ICaptchaService captchaService,
        IMenuQueryService menuQueryService,
        ISystemConfigQueryService systemConfigQueryService,
        IUserAccountRepository userAccountRepository,
        IPasswordHasher passwordHasher,
        IIdGeneratorAccessor idGeneratorAccessor,
        IMapper mapper,
        IValidator<AuthTokenRequest> validator,
        IValidator<AuthRefreshRequest> refreshValidator,
        IValidator<ChangePasswordViewModel> changePasswordValidator,
        IValidator<UserProfileUpdateViewModel> profileUpdateValidator,
        IValidator<RegisterViewModel> registerValidator,
        IAuditRecorder auditRecorder,
        IOptionsMonitor<SecurityOptions> securityOptions,
        ILogger<AuthController> logger)
    {
        _authTokenService = authTokenService;
        _authProfileService = authProfileService;
        _userCommandService = userCommandService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _clientContextAccessor = clientContextAccessor;
        _captchaService = captchaService;
        _menuQueryService = menuQueryService;
        _systemConfigQueryService = systemConfigQueryService;
        _userAccountRepository = userAccountRepository;
        _passwordHasher = passwordHasher;
        _idGeneratorAccessor = idGeneratorAccessor;
        _mapper = mapper;
        _validator = validator;
        _refreshValidator = refreshValidator;
        _changePasswordValidator = changePasswordValidator;
        _profileUpdateValidator = profileUpdateValidator;
        _registerValidator = registerValidator;
        _auditRecorder = auditRecorder;
        _securityOptionsMonitor = securityOptions;
        _logger = logger;
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
            throw new BusinessException("TenantIdRequired", ErrorCodes.ValidationError);
        }

        var dto = _mapper.Map<AuthTokenRequest>(request);
        _validator.ValidateAndThrow(dto);

        // 后端风控：达到失败阈值后必须校验验证码，不能仅依赖前端展示逻辑。
        var securityOptions = _securityOptionsMonitor.CurrentValue;
        var account = await _userAccountRepository.FindByUsernameAsync(tenantId, dto.Username, cancellationToken);
        var requireCaptcha = securityOptions.CaptchaThreshold > 0
            && account is not null
            && account.FailedLoginCount >= securityOptions.CaptchaThreshold;

        if (requireCaptcha)
        {
            if (string.IsNullOrWhiteSpace(dto.CaptchaKey) || string.IsNullOrWhiteSpace(dto.CaptchaCode))
            {
                throw new BusinessException("LoginFailedTooManyTimes", ErrorCodes.ValidationError);
            }

            if (!_captchaService.Validate(dto.CaptchaKey, dto.CaptchaCode))
            {
                throw new BusinessException("CaptchaExpired", ErrorCodes.ValidationError);
            }
        }
        else if (!string.IsNullOrWhiteSpace(dto.CaptchaKey))
        {
            // 若前端已提供验证码（风控触发后），在此处校验
            if (string.IsNullOrWhiteSpace(dto.CaptchaCode)
                || !_captchaService.Validate(dto.CaptchaKey, dto.CaptchaCode))
            {
                throw new BusinessException("CaptchaExpired", ErrorCodes.ValidationError);
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
            throw new BusinessException("TenantIdRequired", ErrorCodes.ValidationError);
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
        var sw = Stopwatch.StartNew();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        _logger.LogInformation("[Auth/Me] 解析CurrentUser 耗时 {Elapsed}ms, UserId={UserId}", sw.ElapsedMilliseconds, currentUser.UserId);

        var profile = await _authProfileService.GetProfileAsync(
            currentUser.UserId,
            currentUser.TenantId,
            cancellationToken);
        _logger.LogInformation("[Auth/Me] GetProfileAsync 耗时 {Elapsed}ms", sw.ElapsedMilliseconds);

        if (profile is null)
        {
            return NotFound(ApiResponse<AuthProfileResult>.Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "UserNotFound"), HttpContext.TraceIdentifier));
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

        _logger.LogInformation("[Auth/Me] 总耗时 {Elapsed}ms", sw.ElapsedMilliseconds);
        return Ok(ApiResponse<AuthProfileResult>.Ok(payloadProfile, HttpContext.TraceIdentifier));
    }

    [HttpGet("profile")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<UserProfileDetailViewModel>>> GetProfile(CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var user = await _userAccountRepository.FindByIdAsync(currentUser.TenantId, currentUser.UserId, cancellationToken);
        if (user is null)
        {
            return NotFound(ApiResponse<UserProfileDetailViewModel>.Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "UserNotFound"), HttpContext.TraceIdentifier));
        }

        var profile = new UserProfileDetailViewModel(
            user.DisplayName,
            user.Email,
            user.PhoneNumber);
        return Ok(ApiResponse<UserProfileDetailViewModel>.Ok(profile, HttpContext.TraceIdentifier));
    }

    [HttpPut("profile")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> UpdateProfile(
        [FromBody] UserProfileUpdateViewModel request,
        CancellationToken cancellationToken)
    {
        _profileUpdateValidator.ValidateAndThrow(request);
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var tenantId = _tenantProvider.GetTenantId();

        await _userCommandService.UpdateProfileAsync(
            tenantId,
            currentUser.UserId,
            request.DisplayName.Trim(),
            string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            cancellationToken);

        var actor = string.IsNullOrWhiteSpace(currentUser.Username) ? currentUser.UserId.ToString() : currentUser.Username;
        var auditContext = new AuditContext(
            currentUser.TenantId,
            actor,
            "UPDATE_PROFILE",
            "SUCCESS",
            null,
            ControllerHelper.GetIpAddress(HttpContext),
            ControllerHelper.GetUserAgent(HttpContext),
            _clientContextAccessor.GetCurrent());
        await _auditRecorder.RecordAsync(auditContext, cancellationToken);

        return Ok(ApiResponse<object>.Ok(new { Success = true }, HttpContext.TraceIdentifier));
    }

    [HttpGet("routers")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<Atlas.Application.Identity.Models.RouterVo>>>> GetRouters(
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();

        var menus = await _menuQueryService.SelectMenuTreeByUserIdAsync(
            currentUser.TenantId,
            currentUser.UserId,
            cancellationToken);
        var routers = _menuQueryService.BuildMenus(menus);

        return Ok(ApiResponse<IReadOnlyList<Atlas.Application.Identity.Models.RouterVo>>.Ok(routers, HttpContext.TraceIdentifier));
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<ApiResponse<object>>> Register(
        [FromBody] RegisterViewModel request,
        CancellationToken cancellationToken)
    {
        _registerValidator.ValidateAndThrow(request);

        var tenantId = _tenantProvider.GetTenantId();
        if (tenantId.IsEmpty)
        {
            throw new BusinessException("TenantIdRequired", ErrorCodes.ValidationError);
        }

        var registerSwitch = await _systemConfigQueryService.GetByKeyAsync(
            tenantId,
            "sys.account.register",
            appId: null,
            cancellationToken);
        if (!string.Equals(registerSwitch?.ConfigValue, "true", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("RegistrationDisabled", ErrorCodes.Forbidden);
        }

        if (!string.IsNullOrWhiteSpace(request.CaptchaKey))
        {
            if (string.IsNullOrWhiteSpace(request.CaptchaCode)
                || !_captchaService.Validate(request.CaptchaKey, request.CaptchaCode))
            {
                throw new BusinessException("CaptchaExpired", ErrorCodes.ValidationError);
            }
        }

        var exists = await _userAccountRepository.ExistsByUsernameAsync(tenantId, request.Username, cancellationToken);
        if (exists)
        {
            throw new BusinessException("UsernameAlreadyExists", ErrorCodes.ValidationError);
        }

        var user = new UserAccount(
            tenantId,
            request.Username.Trim(),
            request.Username.Trim(),
            _passwordHasher.HashPassword(request.Password),
            _idGeneratorAccessor.NextId());
        await _userAccountRepository.AddAsync(user, cancellationToken);

        return Ok(ApiResponse<object>.Ok(new { id = user.Id.ToString() }, HttpContext.TraceIdentifier));
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

