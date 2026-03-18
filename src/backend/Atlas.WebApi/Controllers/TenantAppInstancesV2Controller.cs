using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Models;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v2/tenant-app-instances")]
[Authorize]
public sealed class TenantAppInstancesV2Controller : ControllerBase
{
    private readonly ITenantAppInstanceQueryService _queryService;
    private readonly ITenantAppInstanceCommandService _commandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IValidator<LowCodeAppCreateRequest> _createValidator;
    private readonly IValidator<LowCodeAppUpdateRequest> _updateValidator;
    private readonly IValidator<LowCodeAppImportRequest> _importValidator;

    public TenantAppInstancesV2Controller(
        ITenantAppInstanceQueryService queryService,
        ITenantAppInstanceCommandService commandService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IValidator<LowCodeAppCreateRequest> createValidator,
        IValidator<LowCodeAppUpdateRequest> updateValidator,
        IValidator<LowCodeAppImportRequest> importValidator)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _importValidator = importValidator;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<PagedResult<TenantAppInstanceListItem>>>> Get(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.QueryAsync(tenantId, request, cancellationToken);
        return Ok(ApiResponse<PagedResult<TenantAppInstanceListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<TenantAppInstanceDetail>>> GetById(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetByIdAsync(tenantId, id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<TenantAppInstanceDetail>.Fail(ErrorCodes.NotFound, "Tenant app instance not found.", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<TenantAppInstanceDetail>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("data-source-bindings")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TenantAppDataSourceBinding>>>> GetDataSourceBindings(
        [FromQuery] long[]? appIds,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        IReadOnlyCollection<long>? appInstanceIds = appIds is { Length: > 0 } ? appIds : null;
        var result = await _queryService.GetDataSourceBindingsAsync(tenantId, appInstanceIds, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<TenantAppDataSourceBinding>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] LowCodeAppCreateRequest request,
        CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, "Unauthorized.", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var appId = await _commandService.CreateAsync(
            tenantId,
            currentUser.UserId,
            request,
            cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id = appId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] LowCodeAppUpdateRequest request,
        CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, "Unauthorized.", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdateAsync(
            tenantId,
            currentUser.UserId,
            id,
            request,
            cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/publish")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Publish(
        long id,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, "Unauthorized.", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.PublishAsync(
            tenantId,
            currentUser.UserId,
            id,
            cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(
        long id,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, "Unauthorized.", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.DeleteAsync(
            tenantId,
            currentUser.UserId,
            id,
            cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/export")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult> Export(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var package = await _commandService.ExportAsync(tenantId, id, cancellationToken);
        if (package is null)
        {
            return NotFound(ApiResponse<object>.Fail(ErrorCodes.NotFound, "Tenant app instance not found.", HttpContext.TraceIdentifier));
        }

        var fileName = $"{package.AppKey}-export.json";
        var json = JsonSerializer.Serialize(package, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", fileName);
    }

    [HttpPost("import")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<LowCodeAppImportResult>>> Import(
        [FromBody] LowCodeAppImportRequest request,
        CancellationToken cancellationToken)
    {
        await _importValidator.ValidateAndThrowAsync(request, cancellationToken);
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<LowCodeAppImportResult>.Fail(ErrorCodes.Unauthorized, "Unauthorized.", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _commandService.ImportAsync(
            tenantId,
            currentUser.UserId,
            request,
            cancellationToken);
        return Ok(ApiResponse<LowCodeAppImportResult>.Ok(result, HttpContext.TraceIdentifier));
    }
}
