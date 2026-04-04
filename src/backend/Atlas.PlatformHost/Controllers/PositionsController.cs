using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Application.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Authorization;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.PlatformHost.Controllers;

[ApiController]
[Route("api/v1/positions")]
public sealed class PositionsController : ControllerBase
{
    private readonly IPositionQueryService _positionQueryService;
    private readonly IPositionCommandService _positionCommandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly Atlas.Core.Abstractions.IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IValidator<PositionCreateRequest> _createValidator;
    private readonly IValidator<PositionUpdateRequest> _updateValidator;

    public PositionsController(
        IPositionQueryService positionQueryService,
        IPositionCommandService positionCommandService,
        ITenantProvider tenantProvider,
        Atlas.Core.Abstractions.IIdGeneratorAccessor idGeneratorAccessor,
        IValidator<PositionCreateRequest> createValidator,
        IValidator<PositionUpdateRequest> updateValidator)
    {
        _positionQueryService = positionQueryService;
        _positionCommandService = positionCommandService;
        _tenantProvider = tenantProvider;
        _idGeneratorAccessor = idGeneratorAccessor;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.PositionsView)]
    public async Task<ActionResult<ApiResponse<PagedResult<PositionListItem>>>> Get(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _positionQueryService.QueryPositionsAsync(request, tenantId, cancellationToken);
        return Ok(ApiResponse<PagedResult<PositionListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.PositionsView)]
    public async Task<ActionResult<ApiResponse<PositionDetail>>> GetById(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var detail = await _positionQueryService.GetDetailAsync(id, tenantId, cancellationToken);
        if (detail is null)
        {
            return NotFound(ApiResponse<PositionDetail>.Fail(ErrorCodes.NotFound, "Position not found.", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<PositionDetail>.Ok(detail, HttpContext.TraceIdentifier));
    }

    [HttpGet("all")]
    [Authorize(Policy = PermissionPolicies.PositionsView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PositionListItem>>>> GetAll(
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _positionQueryService.QueryAllAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PositionListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.PositionsCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] PositionCreateRequest request,
        CancellationToken cancellationToken)
    {
        _createValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var id = _idGeneratorAccessor.NextId();
        var createdId = await _positionCommandService.CreateAsync(tenantId, request, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = createdId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.PositionsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] PositionUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _updateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _positionCommandService.UpdateAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.PositionsDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _positionCommandService.DeleteAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }
}




