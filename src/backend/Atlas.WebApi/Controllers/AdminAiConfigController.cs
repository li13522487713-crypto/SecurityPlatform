using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/admin/ai-config")]
[Authorize]
public sealed class AdminAiConfigController : ControllerBase
{
    private readonly IAdminAiConfigService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly IValidator<AdminAiConfigUpdateRequest> _updateValidator;

    public AdminAiConfigController(
        IAdminAiConfigService service,
        ITenantProvider tenantProvider,
        IValidator<AdminAiConfigUpdateRequest> updateValidator)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AiAdminConfigView)]
    public async Task<ActionResult<ApiResponse<AdminAiConfigDto>>> Get(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.GetAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<AdminAiConfigDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPut]
    [Authorize(Policy = PermissionPolicies.AiAdminConfigUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        [FromBody] AdminAiConfigUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        _updateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _service.UpdateAsync(tenantId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Updated = true }, HttpContext.TraceIdentifier));
    }
}
