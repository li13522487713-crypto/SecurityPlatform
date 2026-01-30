using AutoMapper;
using FluentValidation;
using Atlas.Application.Abstractions;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Models;
using Atlas.Core.Exceptions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Audit.Entities;
using Atlas.WebApi.Helpers;
using Atlas.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthTokenService _authTokenService;
    private readonly IAuthProfileService _authProfileService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IMapper _mapper;
    private readonly IValidator<AuthTokenRequest> _validator;
    private readonly IAuditWriter _auditWriter;

    public AuthController(
        IAuthTokenService authTokenService,
        IAuthProfileService authProfileService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IMapper mapper,
        IValidator<AuthTokenRequest> validator,
        IAuditWriter auditWriter)
    {
        _authTokenService = authTokenService;
        _authProfileService = authProfileService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _mapper = mapper;
        _validator = validator;
        _auditWriter = auditWriter;
    }

    [HttpPost("token")]
    [AllowAnonymous]
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

        var context = new AuthRequestContext(
            ControllerHelper.GetIpAddress(HttpContext),
            ControllerHelper.GetUserAgent(HttpContext),
            ControllerHelper.GetClientContext(HttpContext));
        var result = await _authTokenService.CreateTokenAsync(dto, tenantId, context, cancellationToken);
        var payload = ApiResponse<AuthTokenResult>.Ok(result, HttpContext.TraceIdentifier);
        return Ok(payload);
    }

    [HttpPost("refresh")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<AuthTokenResult>>> RefreshToken(CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var context = new AuthRequestContext(
            ControllerHelper.GetIpAddress(HttpContext),
            ControllerHelper.GetUserAgent(HttpContext),
            ControllerHelper.GetClientContext(HttpContext));
        var result = await _authTokenService.CreateTokenForUserAsync(
            currentUser.UserId,
            currentUser.TenantId,
            context,
            cancellationToken);
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

        var clientContext = ControllerHelper.GetClientContext(HttpContext);
        var payloadProfile = profile with { ClientContext = clientContext };
        return Ok(ApiResponse<AuthProfileResult>.Ok(payloadProfile, HttpContext.TraceIdentifier));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> Logout(CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var actor = string.IsNullOrWhiteSpace(currentUser.Username) ? currentUser.UserId.ToString() : currentUser.Username;
        var clientContext = ControllerHelper.GetClientContext(HttpContext);

        var auditRecord = new AuditRecord(
            tenantId: currentUser.TenantId,
            actor: actor,
            action: "LOGOUT",
            result: "SUCCESS",
            target: null,
            ipAddress: ControllerHelper.GetIpAddress(HttpContext),
            userAgent: ControllerHelper.GetUserAgent(HttpContext),
            clientType: clientContext.ClientType.ToString(),
            clientPlatform: clientContext.ClientPlatform.ToString(),
            clientChannel: clientContext.ClientChannel.ToString(),
            clientAgent: clientContext.ClientAgent.ToString());

        await _auditWriter.WriteAsync(auditRecord, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Success = true }, HttpContext.TraceIdentifier));
    }
}
