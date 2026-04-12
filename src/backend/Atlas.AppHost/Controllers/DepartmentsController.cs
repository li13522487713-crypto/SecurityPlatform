using Atlas.Application.Identity;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Atlas.Presentation.Shared.Filters;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/v1/departments")]
public sealed class DepartmentsController : ControllerBase
{
    private readonly IDepartmentQueryService _departmentQueryService;
    private readonly IDepartmentCommandService _departmentCommandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly Atlas.Core.Abstractions.IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IValidator<DepartmentCreateRequest> _createValidator;
    private readonly IValidator<DepartmentUpdateRequest> _updateValidator;

    public DepartmentsController(
        IDepartmentQueryService departmentQueryService,
        IDepartmentCommandService departmentCommandService,
        ITenantProvider tenantProvider,
        Atlas.Core.Abstractions.IIdGeneratorAccessor idGeneratorAccessor,
        IValidator<DepartmentCreateRequest> createValidator,
        IValidator<DepartmentUpdateRequest> updateValidator)
    {
        _departmentQueryService = departmentQueryService;
        _departmentCommandService = departmentCommandService;
        _tenantProvider = tenantProvider;
        _idGeneratorAccessor = idGeneratorAccessor;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.DepartmentsView)]
    public async Task<ActionResult<ApiResponse<PagedResult<DepartmentListItem>>>> Get(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _departmentQueryService.QueryDepartmentsAsync(request, tenantId, cancellationToken);
        return Ok(ApiResponse<PagedResult<DepartmentListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("all")]
    [Authorize(Policy = PermissionPolicies.DepartmentsAll)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DepartmentListItem>>>> GetAll(
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _departmentQueryService.QueryAllAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DepartmentListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.DepartmentsCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] DepartmentCreateRequest request,
        CancellationToken cancellationToken)
    {
        _createValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var id = _idGeneratorAccessor.NextId();
        var createdId = await _departmentCommandService.CreateAsync(tenantId, request, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = createdId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.DepartmentsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] DepartmentUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _updateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _departmentCommandService.UpdateAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.DepartmentsDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _departmentCommandService.DeleteAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }
}
