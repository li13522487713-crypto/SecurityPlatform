using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.PlatformHost.Controllers;

[ApiController]
[Route("api/v1/ai-prompts")]
[Authorize]
public sealed class AiPromptTemplatesController : ControllerBase
{
    private readonly IAiPromptService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly IValidator<AiPromptTemplateCreateRequest> _createValidator;
    private readonly IValidator<AiPromptTemplateUpdateRequest> _updateValidator;

    public AiPromptTemplatesController(
        IAiPromptService service,
        ITenantProvider tenantProvider,
        IValidator<AiPromptTemplateCreateRequest> createValidator,
        IValidator<AiPromptTemplateUpdateRequest> updateValidator)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AiPromptView)]
    public async Task<ActionResult<ApiResponse<PagedResult<AiPromptTemplateListItem>>>> GetPaged(
        [FromQuery] PagedRequest request,
        [FromQuery] string? keyword = null,
        [FromQuery] string? category = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.GetPagedAsync(
            tenantId,
            keyword,
            category,
            request.PageIndex,
            request.PageSize,
            cancellationToken);
        return Ok(ApiResponse<PagedResult<AiPromptTemplateListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AiPromptView)]
    public async Task<ActionResult<ApiResponse<AiPromptTemplateDetail>>> GetById(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.GetByIdAsync(tenantId, id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<AiPromptTemplateDetail>.Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "PromptTemplateNotFound"), HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<AiPromptTemplateDetail>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AiPromptCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] AiPromptTemplateCreateRequest request,
        CancellationToken cancellationToken)
    {
        _createValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var id = await _service.CreateAsync(tenantId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AiPromptUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] AiPromptTemplateUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _updateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _service.UpdateAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AiPromptDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _service.DeleteAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }
}
