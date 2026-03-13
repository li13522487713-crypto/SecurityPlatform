using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/personal-access-tokens")]
[Authorize]
public sealed class PersonalAccessTokensController : ControllerBase
{
    private readonly IPersonalAccessTokenService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IValidator<PersonalAccessTokenCreateRequest> _createValidator;
    private readonly IValidator<PersonalAccessTokenUpdateRequest> _updateValidator;

    public PersonalAccessTokensController(
        IPersonalAccessTokenService service,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IValidator<PersonalAccessTokenCreateRequest> createValidator,
        IValidator<PersonalAccessTokenUpdateRequest> updateValidator)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.PersonalAccessTokenView)]
    public async Task<ActionResult<ApiResponse<PagedResult<PersonalAccessTokenListItem>>>> GetPaged(
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
        return Ok(ApiResponse<PagedResult<PersonalAccessTokenListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.PersonalAccessTokenCreate)]
    public async Task<ActionResult<ApiResponse<PersonalAccessTokenCreateResult>>> Create(
        [FromBody] PersonalAccessTokenCreateRequest request,
        CancellationToken cancellationToken)
    {
        _createValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var result = await _service.CreateAsync(tenantId, currentUser.UserId, request, cancellationToken);
        return Ok(ApiResponse<PersonalAccessTokenCreateResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.PersonalAccessTokenUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] PersonalAccessTokenUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _updateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        await _service.UpdateAsync(tenantId, currentUser.UserId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.PersonalAccessTokenDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Revoke(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        await _service.RevokeAsync(tenantId, currentUser.UserId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }
}
