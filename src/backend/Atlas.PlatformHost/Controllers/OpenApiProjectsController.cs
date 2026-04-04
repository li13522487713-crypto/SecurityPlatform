using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.PlatformHost.Controllers;

[ApiController]
[Route("api/v1/open-api-projects")]
[Authorize]
public sealed class OpenApiProjectsController : ControllerBase
{
    private readonly IOpenApiProjectService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IValidator<OpenApiProjectCreateRequest> _createValidator;
    private readonly IValidator<OpenApiProjectUpdateRequest> _updateValidator;
    private readonly IValidator<OpenApiProjectTokenExchangeRequest> _tokenExchangeValidator;

    public OpenApiProjectsController(
        IOpenApiProjectService service,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IValidator<OpenApiProjectCreateRequest> createValidator,
        IValidator<OpenApiProjectUpdateRequest> updateValidator,
        IValidator<OpenApiProjectTokenExchangeRequest> tokenExchangeValidator)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _tokenExchangeValidator = tokenExchangeValidator;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.PersonalAccessTokenView)]
    public async Task<ActionResult<ApiResponse<PagedResult<OpenApiProjectListItem>>>> GetPaged(
        [FromQuery] PagedRequest request,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var result = await _service.GetPagedAsync(
            tenantId,
            currentUser.UserId,
            keyword,
            request.PageIndex,
            request.PageSize,
            cancellationToken);
        return Ok(ApiResponse<PagedResult<OpenApiProjectListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.PersonalAccessTokenCreate)]
    public async Task<ActionResult<ApiResponse<OpenApiProjectCreateResult>>> Create(
        [FromBody] OpenApiProjectCreateRequest request,
        CancellationToken cancellationToken)
    {
        _createValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var result = await _service.CreateAsync(tenantId, currentUser.UserId, request, cancellationToken);
        return Ok(ApiResponse<OpenApiProjectCreateResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.PersonalAccessTokenUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] OpenApiProjectUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _updateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        await _service.UpdateAsync(tenantId, currentUser.UserId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/rotate-secret")]
    [Authorize(Policy = PermissionPolicies.PersonalAccessTokenUpdate)]
    public async Task<ActionResult<ApiResponse<OpenApiProjectRotateSecretResult>>> RotateSecret(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var result = await _service.RotateSecretAsync(tenantId, currentUser.UserId, id, cancellationToken);
        return Ok(ApiResponse<OpenApiProjectRotateSecretResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.PersonalAccessTokenDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        await _service.DeleteAsync(tenantId, currentUser.UserId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("token")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<OpenApiProjectTokenExchangeResult>>> ExchangeToken(
        [FromBody] OpenApiProjectTokenExchangeRequest request,
        CancellationToken cancellationToken)
    {
        _tokenExchangeValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.ExchangeTokenAsync(tenantId, request, cancellationToken);
        return Ok(ApiResponse<OpenApiProjectTokenExchangeResult>.Ok(result, HttpContext.TraceIdentifier));
    }
}
