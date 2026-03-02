using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Application.Identity.Abstractions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

/// <summary>
/// 字典类型管理（等保2.0：变更需有权限控制，写操作须幂等处理）
/// </summary>
[ApiController]
[Route("api/v1/dict-types")]
public sealed class DictTypesController : ControllerBase
{
    private readonly IDictQueryService _queryService;
    private readonly IDictCommandService _commandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly IValidator<DictTypeCreateRequest> _createValidator;
    private readonly IValidator<DictTypeUpdateRequest> _updateValidator;
    private readonly IValidator<DictDataCreateRequest> _createDataValidator;
    private readonly IExcelExportService _excelExportService;

    public DictTypesController(
        IDictQueryService queryService,
        IDictCommandService commandService,
        ITenantProvider tenantProvider,
        IValidator<DictTypeCreateRequest> createValidator,
        IValidator<DictTypeUpdateRequest> updateValidator,
        IValidator<DictDataCreateRequest> createDataValidator,
        IExcelExportService excelExportService)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tenantProvider = tenantProvider;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _createDataValidator = createDataValidator;
        _excelExportService = excelExportService;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.DictTypeView)]
    public async Task<ActionResult<ApiResponse<PagedResult<DictTypeDto>>>> Get(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetDictTypesPagedAsync(tenantId, keyword, pageIndex, pageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<DictTypeDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("all")]
    [Authorize(Policy = PermissionPolicies.DictTypeView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DictTypeDto>>>> GetAll(CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetAllActiveDictTypesAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DictTypeDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.DictTypeView)]
    public async Task<ActionResult<ApiResponse<DictTypeDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetDictTypeByIdAsync(tenantId, id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<DictTypeDto>.Fail(ErrorCodes.NotFound, "字典类型不存在", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<DictTypeDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{code}/data")]
    [Authorize(Policy = PermissionPolicies.DictDataView)]
    public async Task<ActionResult<ApiResponse<PagedResult<DictDataDto>>>> GetData(
        string code,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetDictDataPagedAsync(tenantId, code, keyword, pageIndex, pageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<DictDataDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{code}/data/active")]
    [Authorize(Policy = PermissionPolicies.DictDataView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DictDataDto>>>> GetActiveData(
        string code,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetActiveDictDataByCodeAsync(tenantId, code, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DictDataDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{code}/data/export")]
    [Authorize(Policy = PermissionPolicies.DictDataView)]
    public async Task<IActionResult> ExportData(
        string code,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var bytes = await _excelExportService.ExportDictDataAsync(tenantId, code, keyword, cancellationToken);
        var fileName = $"dict_{code}_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.DictTypeCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] DictTypeCreateRequest request,
        CancellationToken cancellationToken)
    {
        _createValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var id = await _commandService.CreateDictTypeAsync(tenantId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.DictTypeUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] DictTypeUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _updateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdateDictTypeAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.DictTypeDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.DeleteDictTypeAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{code}/data")]
    [Authorize(Policy = PermissionPolicies.DictDataCreate)]
    public async Task<ActionResult<ApiResponse<object>>> CreateData(
        string code,
        [FromBody] DictDataCreateRequest request,
        CancellationToken cancellationToken)
    {
        _createDataValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var id = await _commandService.CreateDictDataAsync(tenantId, code, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }
}
