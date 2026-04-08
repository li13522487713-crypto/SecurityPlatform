using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.AppHost.Controllers;

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
    private readonly IValidator<ModelConfigPromptTestRequest> _promptTestValidator;

    public ModelConfigsController(
        IModelConfigQueryService queryService,
        IModelConfigCommandService commandService,
        ITenantProvider tenantProvider,
        IValidator<ModelConfigCreateRequest> createValidator,
        IValidator<ModelConfigUpdateRequest> updateValidator,
        IValidator<ModelConfigTestRequest> testValidator,
        IValidator<ModelConfigPromptTestRequest> promptTestValidator)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tenantProvider = tenantProvider;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _testValidator = testValidator;
        _promptTestValidator = promptTestValidator;
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

    [HttpGet("stats")]
    [Authorize(Policy = PermissionPolicies.ModelConfigView)]
    public async Task<ActionResult<ApiResponse<ModelConfigStatsDto>>> GetStats(
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetStatsAsync(tenantId, keyword, cancellationToken);
        return Ok(ApiResponse<ModelConfigStatsDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.ModelConfigView)]
    public async Task<ActionResult<ApiResponse<ModelConfigDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetByIdAsync(tenantId, id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<ModelConfigDto>.Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "ModelConfigDtoNotFound"), HttpContext.TraceIdentifier));
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
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _commandService.TestConnectionAsync(tenantId, request, cancellationToken);
        return Ok(ApiResponse<ModelConfigTestResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("test/stream")]
    [Authorize(Policy = PermissionPolicies.ModelConfigCreate)]
    public async Task TestPromptStream(
        [FromBody] ModelConfigPromptTestRequest request,
        CancellationToken cancellationToken)
    {
        _promptTestValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        await foreach (var evt in _commandService.TestPromptStreamAsync(tenantId, request, cancellationToken))
        {
            await WriteSseTypedEventAsync(Response, evt.EventType, evt.Data, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }

        await WriteSseTypedEventAsync(Response, "done", "[DONE]", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    private static async Task WriteSseTypedEventAsync(
        HttpResponse response,
        string eventType,
        string payload,
        CancellationToken cancellationToken)
    {
        await response.WriteAsync($"event: {eventType}\n", cancellationToken);
        await WriteSseDataEventAsync(response, payload, cancellationToken);
    }

    private static async Task WriteSseDataEventAsync(HttpResponse response, string payload, CancellationToken cancellationToken)
    {
        var normalized = payload.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');
        foreach (var line in lines)
        {
            await response.WriteAsync($"data: {line}\n", cancellationToken);
        }

        await response.WriteAsync("\n", cancellationToken);
    }
}
