using Atlas.Application.Integration;
using Atlas.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.WebApi.Filters;

namespace Atlas.WebApi.Controllers;

/// <summary>
/// OpenAPI 连接器管理 API
/// </summary>
[ApiController]
[Route("api/v1/connectors")]
[Authorize]
[PlatformOnly]
public sealed class ApiConnectorsController : ControllerBase
{
    private readonly IApiConnectorService _service;

    public ApiConnectorsController(IApiConnectorService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> GetAll(CancellationToken cancellationToken = default)
    {
        var connectors = await _service.GetAllAsync(cancellationToken);
        return Ok(ApiResponse<object>.Ok(connectors, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> GetById(long id, CancellationToken cancellationToken = default)
    {
        var connector = await _service.GetByIdAsync(id, cancellationToken);
        if (connector is null)
        {
            return NotFound(ApiResponse<object>.Fail("NOT_FOUND", ApiResponseLocalizer.T(HttpContext, "ApiConnectorNotFound"), HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<object>.Ok(connector, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] CreateApiConnectorRequest request,
        CancellationToken cancellationToken = default)
    {
        var id = await _service.CreateAsync(request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] UpdateApiConnectorRequest request,
        CancellationToken cancellationToken = default)
    {
        await _service.UpdateAsync(id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken = default)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/operations")]
    public async Task<ActionResult<ApiResponse<object>>> GetOperations(long id, CancellationToken cancellationToken = default)
    {
        var ops = await _service.GetOperationsAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(ops, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/sync")]
    public async Task<ActionResult<ApiResponse<object>>> SyncSpec(long id, CancellationToken cancellationToken = default)
    {
        await _service.SyncFromSpecAsync(id, cancellationToken);
        var ops = await _service.GetOperationsAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Count = ops.Count }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/operations/{operationId}/execute")]
    public async Task<ActionResult<ApiResponse<object>>> Execute(
        long id,
        string operationId,
        [FromBody] ExecuteOperationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.ExecuteAsync(
            id, operationId,
            request.PathParams ?? new(),
            request.QueryParams ?? new(),
            request.Body,
            cancellationToken);

        return Ok(ApiResponse<object>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/health")]
    public async Task<ActionResult<ApiResponse<object>>> Health(long id, CancellationToken cancellationToken = default)
    {
        var healthy = await _service.HealthCheckAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Healthy = healthy }, HttpContext.TraceIdentifier));
    }
}

public sealed record ExecuteOperationRequest(
    Dictionary<string, string?>? PathParams,
    Dictionary<string, string?>? QueryParams,
    string? Body);
