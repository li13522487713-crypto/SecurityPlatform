using Atlas.Application.License.Abstractions;
using Atlas.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.PlatformHost.Controllers;

[ApiController]
[Route("api/v1/license")]
[Obsolete("Deprecated since Sprint 1. Please migrate to api/v1/licenses endpoints.")]
public sealed class LicenseController : ControllerBase
{
    private readonly ILicenseService _licenseService;
    private readonly ILicenseActivationService _activationService;
    private readonly IMachineFingerprintService _fingerprintService;

    public LicenseController(
        ILicenseService licenseService,
        ILicenseActivationService activationService,
        IMachineFingerprintService fingerprintService)
    {
        _licenseService = licenseService;
        _activationService = activationService;
        _fingerprintService = fingerprintService;
    }

    /// <summary>获取当前授权状态</summary>
    [HttpGet("status")]
    [AllowAnonymous]
    public ActionResult<ApiResponse<object>> GetStatus()
    {
        var status = _licenseService.GetCurrentStatus();
        return Ok(ApiResponse<object>.Ok(new
        {
            status = status.Status,
            edition = status.Edition,
            isPermanent = status.IsPermanent,
            issuedAt = status.IssuedAt == DateTimeOffset.MinValue ? (DateTimeOffset?)null : status.IssuedAt,
            expiresAt = status.ExpiresAt,
            remainingDays = status.RemainingDays,
            machineBound = status.MachineBound,
            machineMatched = status.MachineMatched,
            features = status.Features,
            limits = status.Limits,
            tenantId = status.TenantId,
            tenantName = status.TenantName
        }, HttpContext.TraceIdentifier));
    }

    /// <summary>获取当前机器码（供颁发工具绑定机器使用）</summary>
    [HttpGet("fingerprint")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public ActionResult<ApiResponse<object>> GetFingerprint()
    {
        var fingerprint = _fingerprintService.GetCurrentFingerprint();
        return Ok(ApiResponse<object>.Ok(new { fingerprint }, HttpContext.TraceIdentifier));
    }

    /// <summary>上传授权证书并激活（AllowAnonymous，支持在登录前激活）</summary>
    [HttpPost("activate")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<ApiResponse<object>>> Activate(
        [FromBody] LicenseActivateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.LicenseContent))
        {
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.ValidationError, ApiResponseLocalizer.T(HttpContext, "LicenseCertificateRequired"), HttpContext.TraceIdentifier));
        }

        var result = await _activationService.ActivateAsync(request.LicenseContent, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.LicenseInvalid, result.Message, HttpContext.TraceIdentifier));
        }

        var status = _licenseService.GetCurrentStatus();
        return Ok(ApiResponse<object>.Ok(new
        {
            message = result.Message,
            edition = status.Edition,
            isPermanent = status.IsPermanent,
            expiresAt = status.ExpiresAt,
            remainingDays = status.RemainingDays,
            tenantId = status.TenantId,
            tenantName = status.TenantName
        }, HttpContext.TraceIdentifier));
    }
}

public sealed record LicenseActivateRequest(string LicenseContent);
