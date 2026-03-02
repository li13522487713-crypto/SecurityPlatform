using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/dict-data")]
public sealed class DictDataController : ControllerBase
{
    private readonly IDictQueryService _queryService;
    private readonly IDictCommandService _commandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly IValidator<DictDataUpdateRequest> _updateValidator;

    public DictDataController(
        IDictQueryService queryService,
        IDictCommandService commandService,
        ITenantProvider tenantProvider,
        IValidator<DictDataUpdateRequest> updateValidator)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tenantProvider = tenantProvider;
        _updateValidator = updateValidator;
    }

    /// <summary>按字典类型编码查询所有启用数据（供前端下拉使用）</summary>
    [HttpGet("by-code/{code}")]
    [Authorize(Policy = PermissionPolicies.DictDataView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DictDataDto>>>> GetByCode(
        string code,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetActiveDictDataByCodeAsync(tenantId, code, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DictDataDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.DictDataUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] DictDataUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _updateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdateDictDataAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.DictDataDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.DeleteDictDataAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }
}
