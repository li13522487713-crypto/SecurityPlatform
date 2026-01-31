using Atlas.Application.DynamicTables.Abstractions;
using Atlas.Application.DynamicTables.Models;
using Atlas.Core.Models;
using Atlas.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/dynamic/meta")]
public sealed class DynamicMetaController : ControllerBase
{
    private readonly IDynamicTableQueryService _queryService;

    public DynamicMetaController(IDynamicTableQueryService queryService)
    {
        _queryService = queryService;
    }

    [HttpGet("field-types")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DynamicFieldTypeOption>>>> GetFieldTypes(
        [FromQuery] string dbType,
        CancellationToken cancellationToken)
    {
        var options = await _queryService.GetFieldTypesAsync(dbType, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DynamicFieldTypeOption>>.Ok(options, HttpContext.TraceIdentifier));
    }
}
