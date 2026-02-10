using Atlas.Application.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/mfa")]
[Authorize]
public sealed class MfaController : ControllerBase
{
    private readonly ITotpService _totpService;
    private readonly IUserAccountRepository _userRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public MfaController(
        ITotpService totpService,
        IUserAccountRepository userRepository,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _totpService = totpService;
        _userRepository = userRepository;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    /// <summary>
    /// Initiates MFA setup by generating a TOTP secret and provisioning URI.
    /// The user should scan the QR code with an authenticator app.
    /// </summary>
    [HttpPost("setup")]
    public async Task<ActionResult<ApiResponse<MfaSetupResult>>> Setup(CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var tenantId = _tenantProvider.GetTenantId();

        var user = await _userRepository.FindByIdAsync(tenantId, currentUser.UserId, cancellationToken);
        if (user is null)
        {
            throw new BusinessException("用户不存在", ErrorCodes.NotFound);
        }

        if (user.MfaEnabled)
        {
            throw new BusinessException("MFA已启用，请先禁用后重新设置", ErrorCodes.ValidationError);
        }

        var secretKey = _totpService.GenerateSecretKey();
        user.SetupMfa(secretKey);
        await _userRepository.UpdateAsync(user, cancellationToken);

        var provisioningUri = _totpService.GenerateProvisioningUri(secretKey, user.Username, "Atlas Security Platform");

        var result = new MfaSetupResult(secretKey, provisioningUri);
        return Ok(ApiResponse<MfaSetupResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// Verifies the TOTP code to confirm MFA setup, then enables MFA for the user.
    /// </summary>
    [HttpPost("verify-setup")]
    public async Task<ActionResult<ApiResponse<object>>> VerifySetup(
        [FromBody] MfaVerifyRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var tenantId = _tenantProvider.GetTenantId();

        var user = await _userRepository.FindByIdAsync(tenantId, currentUser.UserId, cancellationToken);
        if (user is null)
        {
            throw new BusinessException("用户不存在", ErrorCodes.NotFound);
        }

        if (user.MfaEnabled)
        {
            throw new BusinessException("MFA已启用", ErrorCodes.ValidationError);
        }

        if (string.IsNullOrWhiteSpace(user.MfaSecretKey))
        {
            throw new BusinessException("请先调用 setup 接口生成密钥", ErrorCodes.ValidationError);
        }

        if (!_totpService.ValidateCode(user.MfaSecretKey, request.Code))
        {
            throw new BusinessException("验证码错误，请重试", ErrorCodes.ValidationError);
        }

        user.EnableMfa();
        await _userRepository.UpdateAsync(user, cancellationToken);

        return Ok(ApiResponse<object>.Ok(new { MfaEnabled = true }, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// Disables MFA for the current user. Requires the current TOTP code for verification.
    /// </summary>
    [HttpPost("disable")]
    public async Task<ActionResult<ApiResponse<object>>> Disable(
        [FromBody] MfaVerifyRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var tenantId = _tenantProvider.GetTenantId();

        var user = await _userRepository.FindByIdAsync(tenantId, currentUser.UserId, cancellationToken);
        if (user is null)
        {
            throw new BusinessException("用户不存在", ErrorCodes.NotFound);
        }

        if (!user.MfaEnabled || string.IsNullOrWhiteSpace(user.MfaSecretKey))
        {
            throw new BusinessException("MFA未启用", ErrorCodes.ValidationError);
        }

        if (!_totpService.ValidateCode(user.MfaSecretKey, request.Code))
        {
            throw new BusinessException("验证码错误", ErrorCodes.ValidationError);
        }

        user.DisableMfa();
        await _userRepository.UpdateAsync(user, cancellationToken);

        return Ok(ApiResponse<object>.Ok(new { MfaEnabled = false }, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// Gets the current MFA status for the authenticated user.
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult<ApiResponse<MfaStatusResult>>> Status(CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var tenantId = _tenantProvider.GetTenantId();

        var user = await _userRepository.FindByIdAsync(tenantId, currentUser.UserId, cancellationToken);
        if (user is null)
        {
            throw new BusinessException("用户不存在", ErrorCodes.NotFound);
        }

        var result = new MfaStatusResult(user.MfaEnabled);
        return Ok(ApiResponse<MfaStatusResult>.Ok(result, HttpContext.TraceIdentifier));
    }
}

public sealed record MfaSetupResult(string SecretKey, string ProvisioningUri);
public sealed record MfaVerifyRequest(string Code);
public sealed record MfaStatusResult(bool MfaEnabled);
