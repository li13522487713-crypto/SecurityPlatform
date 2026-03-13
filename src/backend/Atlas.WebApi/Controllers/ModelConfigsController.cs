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
[Route("api/v1/model-configs")]
public sealed class ModelConfigsController : ControllerBase
{
    private readonly IModelConfigQueryService _queryService;
    private readonly IModelConfigCommandService _commandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly IValidator<ModelConfigCreateRequest> _createValidator;
    private readonly IValidator<ModelConfigUpdateRequest> _updateValidator;
    private readonly IValidator<ModelConfigTestRequest> _testValidator;

    public ModelConfigsController(
        IModelConfigQueryService queryService,
        IModelConfigCommandService commandService,
        ITenantProvider tenantProvider,
        IValidator<ModelConfigCreateRequest> createValidator,
        IValidator<ModelConfigUpdateRequest> updateValidator,
        IValidator<ModelConfigTestRequest> testValidator)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tenantProvider = tenantProvider;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _testValidator = testValidator;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.ModelConfigView)]
    public async Task<ActionResult<ApiResponse<PagedResult<ModelConfigDto>>>> GetPaged(
        [FromQuery] PagedRequest request,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetPagedAsync(tenantId, keyword, request.PageIndex, request.PageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<ModelConfigDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("enabled")]
    [Authorize(Policy = PermissionPolicies.ModelConfigView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ModelConfigDto>>>> GetEnabled(CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetAllEnabledAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ModelConfigDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.ModelConfigView)]
    public async Task<ActionResult<ApiResponse<ModelConfigDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetByIdAsync(tenantId, id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<ModelConfigDto>.Fail(ErrorCodes.NotFound, "模型配置不存在", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<ModelConfigDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.ModelConfigCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] ModelConfigCreateRequest request,
        CancellationToken cancellationToken)
    {
        _createValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var id = await _commandService.CreateAsync(tenantId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.ModelConfigUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] ModelConfigUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _updateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdateAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.ModelConfigDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.DeleteAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("test")]
    [Authorize(Policy = PermissionPolicies.ModelConfigCreate)]
    public async Task<ActionResult<ApiResponse<ModelConfigTestResult>>> Test(
        [FromBody] ModelConfigTestRequest request,
        CancellationToken cancellationToken)
    {
        _testValidator.ValidateAndThrow(request);
        var result = await _commandService.TestConnectionAsync(request, cancellationToken);
        return Ok(ApiResponse<ModelConfigTestResult>.Ok(result, HttpContext.TraceIdentifier));
    }
}
