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
[Route("api/v1/ai-plugins")]
[Authorize]
public sealed class AiPluginsController : ControllerBase
{
    private readonly IAiPluginService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly IValidator<AiPluginCreateRequest> _createValidator;
    private readonly IValidator<AiPluginUpdateRequest> _updateValidator;
    private readonly IValidator<AiPluginDebugRequest> _debugValidator;
    private readonly IValidator<AiPluginOpenApiImportRequest> _importValidator;
    private readonly IValidator<AiPluginApiCreateRequest> _apiCreateValidator;
    private readonly IValidator<AiPluginApiUpdateRequest> _apiUpdateValidator;

    public AiPluginsController(
        IAiPluginService service,
        ITenantProvider tenantProvider,
        IValidator<AiPluginCreateRequest> createValidator,
        IValidator<AiPluginUpdateRequest> updateValidator,
        IValidator<AiPluginDebugRequest> debugValidator,
        IValidator<AiPluginOpenApiImportRequest> importValidator,
        IValidator<AiPluginApiCreateRequest> apiCreateValidator,
        IValidator<AiPluginApiUpdateRequest> apiUpdateValidator)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _debugValidator = debugValidator;
        _importValidator = importValidator;
        _apiCreateValidator = apiCreateValidator;
        _apiUpdateValidator = apiUpdateValidator;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AiPluginView)]
    public async Task<ActionResult<ApiResponse<PagedResult<AiPluginListItem>>>> GetPaged(
        [FromQuery] PagedRequest request,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.GetPagedAsync(tenantId, keyword, request.PageIndex, request.PageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<AiPluginListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("built-in-metadata")]
    [Authorize(Policy = PermissionPolicies.AiPluginView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AiPluginBuiltInMetaItem>>>> GetBuiltInMetadata(
        CancellationToken cancellationToken)
    {
        var result = await _service.GetBuiltInMetadataAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AiPluginBuiltInMetaItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AiPluginView)]
    public async Task<ActionResult<ApiResponse<AiPluginDetail>>> GetById(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.GetByIdAsync(tenantId, id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<AiPluginDetail>.Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "AiPluginDetailNotFound"), HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<AiPluginDetail>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AiPluginCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] AiPluginCreateRequest request,
        CancellationToken cancellationToken)
    {
        _createValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var id = await _service.CreateAsync(tenantId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AiPluginUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] AiPluginUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _updateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _service.UpdateAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AiPluginDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _service.DeleteAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/publish")]
    [Authorize(Policy = PermissionPolicies.AiPluginPublish)]
    public async Task<ActionResult<ApiResponse<object>>> Publish(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _service.PublishAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPatch("{id:long}/lock")]
    [Authorize(Policy = PermissionPolicies.AiPluginUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> SetLock(
        long id,
        [FromBody] AiPluginLockRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _service.SetLockAsync(tenantId, id, request.IsLocked, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString(), request.IsLocked }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/debug")]
    [Authorize(Policy = PermissionPolicies.AiPluginDebug)]
    public async Task<ActionResult<ApiResponse<AiPluginDebugResult>>> Debug(
        long id,
        [FromBody] AiPluginDebugRequest request,
        CancellationToken cancellationToken)
    {
        _debugValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.DebugAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<AiPluginDebugResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/import/openapi")]
    [Authorize(Policy = PermissionPolicies.AiPluginUpdate)]
    public async Task<ActionResult<ApiResponse<AiPluginOpenApiImportResult>>> ImportOpenApi(
        long id,
        [FromBody] AiPluginOpenApiImportRequest request,
        CancellationToken cancellationToken)
    {
        _importValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.ImportOpenApiAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<AiPluginOpenApiImportResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/apis")]
    [Authorize(Policy = PermissionPolicies.AiPluginView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AiPluginApiItem>>>> GetApis(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.GetApisAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AiPluginApiItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/apis")]
    [Authorize(Policy = PermissionPolicies.AiPluginCreate)]
    public async Task<ActionResult<ApiResponse<object>>> CreateApi(
        long id,
        [FromBody] AiPluginApiCreateRequest request,
        CancellationToken cancellationToken)
    {
        _apiCreateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var apiId = await _service.CreateApiAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = apiId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}/apis/{apiId:long}")]
    [Authorize(Policy = PermissionPolicies.AiPluginUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateApi(
        long id,
        long apiId,
        [FromBody] AiPluginApiUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _apiUpdateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _service.UpdateApiAsync(tenantId, id, apiId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = apiId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}/apis/{apiId:long}")]
    [Authorize(Policy = PermissionPolicies.AiPluginDelete)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteApi(
        long id,
        long apiId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _service.DeleteApiAsync(tenantId, id, apiId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = apiId.ToString() }, HttpContext.TraceIdentifier));
    }
}
