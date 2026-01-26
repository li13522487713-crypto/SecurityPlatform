using AutoMapper;
using FluentValidation;
using Atlas.Application.Abstractions;
using Atlas.Application.Models;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthTokenService _authTokenService;
    private readonly ITenantProvider _tenantProvider;
    private readonly IMapper _mapper;
    private readonly IValidator<AuthTokenRequest> _validator;

    public AuthController(
        IAuthTokenService authTokenService,
        ITenantProvider tenantProvider,
        IMapper mapper,
        IValidator<AuthTokenRequest> validator)
    {
        _authTokenService = authTokenService;
        _tenantProvider = tenantProvider;
        _mapper = mapper;
        _validator = validator;
    }

    [HttpPost("token")]
    [AllowAnonymous]
    public ActionResult<ApiResponse<AuthTokenResult>> CreateToken([FromBody] AuthTokenViewModel request)
    {
        var tenantId = _tenantProvider.GetTenantId();
        if (tenantId.IsEmpty)
        {
            throw new BusinessException("缺少租户标识", ErrorCodes.ValidationError);
        }

        var dto = _mapper.Map<AuthTokenRequest>(request);
        _validator.ValidateAndThrow(dto);

        var result = _authTokenService.CreateToken(dto, tenantId);
        var payload = ApiResponse<AuthTokenResult>.Ok(result, HttpContext.TraceIdentifier);
        return Ok(payload);
    }
}