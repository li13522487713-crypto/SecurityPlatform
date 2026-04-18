using System.Text;
using Atlas.Application.Identity.Abstractions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers.Sso;

/// <summary>
/// 治理 M-G07-C4（S14）：SAML SP 端点骨架。
///
/// 真实 SAML AuthnRequest / Response 解析与签名校验需要 ITfoxtec.Identity.Saml2.MvcCore（M-G07-C1，已立项 follow-up）；
/// 当前 controller 提供：
/// 1. /sso/saml/{tenantId}/metadata：基于 TenantIdentityProvider.ConfigJson 拼装最小 SP metadata XML，便于 IdP 端导入；
/// 2. /sso/saml/{tenantId}/login / acs / slo：返回 501 Not Implemented + 明确指引，避免误导部署方接到端点后认为流程已通。
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("sso/saml/{tenantId:guid}")]
public sealed class SamlSpController : ControllerBase
{
    private readonly ITenantIdentityProviderService _idpService;

    public SamlSpController(ITenantIdentityProviderService idpService)
    {
        _idpService = idpService;
    }

    [HttpGet("metadata")]
    public async Task<IActionResult> Metadata(Guid tenantId, [FromQuery] string idpCode = "saml", CancellationToken cancellationToken = default)
    {
        var dto = await _idpService.GetByCodeAsync(new TenantId(tenantId), idpCode, cancellationToken);
        if (dto is null || !string.Equals(dto.IdpType, "saml", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(new { code = "TENANT_IDP_NOT_FOUND", idpCode });
        }

        var entityId = $"https://atlas/sso/saml/{tenantId:N}";
        var acsUrl = $"https://atlas/sso/saml/{tenantId:N}/acs";
        var sloUrl = $"https://atlas/sso/saml/{tenantId:N}/slo";
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.Append("<EntityDescriptor xmlns=\"urn:oasis:names:tc:SAML:2.0:metadata\" entityID=\"").Append(entityId).Append("\">");
        sb.Append("<SPSSODescriptor protocolSupportEnumeration=\"urn:oasis:names:tc:SAML:2.0:protocol\">");
        sb.Append("<AssertionConsumerService index=\"0\" Binding=\"urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST\" Location=\"").Append(acsUrl).Append("\"/>");
        sb.Append("<SingleLogoutService Binding=\"urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST\" Location=\"").Append(sloUrl).Append("\"/>");
        sb.Append("</SPSSODescriptor>");
        sb.Append("</EntityDescriptor>");
        return Content(sb.ToString(), "application/samlmetadata+xml", Encoding.UTF8);
    }

    [HttpGet("login")]
    public IActionResult Login(Guid tenantId)
    {
        return StatusCode(StatusCodes.Status501NotImplemented, ApiResponse<object>.Fail(
            "SAML_NOT_INSTALLED",
            "SAML SP login pending ITfoxtec.Identity.Saml2.MvcCore wiring (M-G07-C1 follow-up). Tenant IdP record found but runtime is not configured.",
            HttpContext.TraceIdentifier));
    }

    [HttpPost("acs")]
    public IActionResult Acs(Guid tenantId)
    {
        return StatusCode(StatusCodes.Status501NotImplemented, ApiResponse<object>.Fail(
            "SAML_NOT_INSTALLED",
            "SAML AssertionConsumerService pending ITfoxtec.Identity.Saml2.MvcCore wiring (M-G07-C1 follow-up).",
            HttpContext.TraceIdentifier));
    }

    [HttpPost("slo")]
    public IActionResult Slo(Guid tenantId)
    {
        return StatusCode(StatusCodes.Status501NotImplemented, ApiResponse<object>.Fail(
            "SAML_NOT_INSTALLED",
            "SAML SingleLogoutService pending ITfoxtec.Identity.Saml2.MvcCore wiring (M-G07-C1 follow-up).",
            HttpContext.TraceIdentifier));
    }
}
