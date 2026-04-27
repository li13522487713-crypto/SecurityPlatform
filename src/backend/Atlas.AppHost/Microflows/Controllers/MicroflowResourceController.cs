using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Exceptions;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Microflows.Controllers;

[Route("api/microflows")]
[AllowAnonymous]
public sealed class MicroflowResourceController : MicroflowApiControllerBase
{
    private readonly IMicroflowResourceQueryService _resourceQueryService;
    private readonly IMicroflowValidationService _validationService;
    private readonly IMicroflowRuntimeSkeletonService _runtimeService;
    private readonly IMicroflowStorageDiagnosticsService _storageDiagnosticsService;
    private readonly IMicroflowRequestContextAccessor _requestContextAccessor;

    public MicroflowResourceController(
        IMicroflowResourceQueryService resourceQueryService,
        IMicroflowValidationService validationService,
        IMicroflowRuntimeSkeletonService runtimeService,
        IMicroflowStorageDiagnosticsService storageDiagnosticsService,
        IMicroflowRequestContextAccessor requestContextAccessor)
        : base(requestContextAccessor)
    {
        _resourceQueryService = resourceQueryService;
        _validationService = validationService;
        _runtimeService = runtimeService;
        _storageDiagnosticsService = storageDiagnosticsService;
        _requestContextAccessor = requestContextAccessor;
    }

    [HttpGet("health")]
    [ProducesResponseType(typeof(MicroflowApiResponse<MicroflowHealthDto>), StatusCodes.Status200OK)]
    public ActionResult<MicroflowApiResponse<MicroflowHealthDto>> GetHealth()
    {
        var traceId = TraceId;
        return MicroflowOk(new MicroflowHealthDto
        {
            Status = "ok",
            Service = "microflows",
            Version = "backend-skeleton",
            Timestamp = DateTimeOffset.UtcNow,
            TraceId = traceId
        });
    }

    [HttpGet("storage/health")]
    [ProducesResponseType(typeof(MicroflowApiResponse<MicroflowStorageHealthDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<MicroflowStorageHealthDto>>> GetStorageHealth(
        CancellationToken cancellationToken)
    {
        var result = await _storageDiagnosticsService.GetHealthAsync(cancellationToken);
        return MicroflowOk(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(MicroflowApiResponse<MicroflowApiPageResult<MicroflowResourceDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<MicroflowApiPageResult<MicroflowResourceDto>>>> GetPaged(
        [FromQuery] string? workspaceId,
        [FromQuery] string? keyword,
        [FromQuery] string[]? status,
        [FromQuery] string[]? publishStatus,
        [FromQuery] bool favoriteOnly = false,
        [FromQuery] string? moduleId = null,
        [FromQuery] string[]? tags = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = null,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var current = _requestContextAccessor.Current;
        var result = await _resourceQueryService.GetPagedAsync(
            new MicroflowResourceQueryDto
            {
                WorkspaceId = workspaceId ?? current.WorkspaceId,
                TenantId = current.TenantId,
                Keyword = keyword,
                Status = status ?? Array.Empty<string>(),
                PublishStatus = publishStatus ?? Array.Empty<string>(),
                FavoriteOnly = favoriteOnly,
                ModuleId = moduleId,
                Tags = tags ?? Array.Empty<string>(),
                SortBy = sortBy,
                SortOrder = sortOrder,
                PageIndex = pageIndex,
                PageSize = pageSize
            },
            cancellationToken);

        return MicroflowOk(result);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(MicroflowApiResponse<MicroflowResourceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MicroflowApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MicroflowApiResponse<MicroflowResourceDto>>> GetById(
        string id,
        CancellationToken cancellationToken)
    {
        var resource = await _resourceQueryService.GetByIdAsync(id, cancellationToken)
            ?? throw new MicroflowApiException(
                MicroflowApiErrorCode.MicroflowNotFound,
                "微流资源不存在。",
                StatusCodes.Status404NotFound);

        return MicroflowOk(resource);
    }

    [HttpPost]
    [ProducesResponseType(typeof(MicroflowApiResponse<object>), StatusCodes.Status503ServiceUnavailable)]
    public ActionResult<MicroflowApiResponse<object>> Create()
    {
        return ServiceUnavailable("微流资源创建尚未启用，将在 Resource CRUD 轮次实现。");
    }

    [HttpPut("{id}/schema")]
    [ProducesResponseType(typeof(MicroflowApiResponse<object>), StatusCodes.Status503ServiceUnavailable)]
    public ActionResult<MicroflowApiResponse<object>> SaveSchema(string id)
    {
        return ServiceUnavailable("微流 Schema 保存尚未启用，将在 Schema 存储轮次实现。");
    }

    [HttpPost("{id}/validate")]
    [ProducesResponseType(typeof(MicroflowApiResponse<ValidateMicroflowResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<ValidateMicroflowResponseDto>>> Validate(
        string id,
        [FromBody] ValidateMicroflowRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _validationService.ValidateAsync(id, request, cancellationToken);
        return MicroflowOk(result);
    }

    [HttpPost("{id}/test-run")]
    [ProducesResponseType(typeof(MicroflowApiResponse<object>), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<MicroflowApiResponse<object>>> TestRun(
        string id,
        [FromBody] TestRunMicroflowRequestDto request,
        CancellationToken cancellationToken)
    {
        await _runtimeService.TestRunAsync(id, request, cancellationToken);
        return MicroflowOk<object>(new { id });
    }

    [HttpGet("runs/{runId}")]
    [ProducesResponseType(typeof(MicroflowApiResponse<MicroflowRunSessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MicroflowApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MicroflowApiResponse<MicroflowRunSessionDto>>> GetRun(
        string runId,
        CancellationToken cancellationToken)
    {
        var result = await _runtimeService.GetRunAsync(runId, cancellationToken);
        return MicroflowOk(result);
    }

    [HttpGet("runs/{runId}/trace")]
    [ProducesResponseType(typeof(MicroflowApiResponse<MicroflowRunTraceResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MicroflowApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MicroflowApiResponse<MicroflowRunTraceResponseDto>>> GetTrace(
        string runId,
        CancellationToken cancellationToken)
    {
        var result = await _runtimeService.GetTraceAsync(runId, cancellationToken);
        return MicroflowOk(result);
    }

    [HttpPost("runs/{runId}/cancel")]
    [ProducesResponseType(typeof(MicroflowApiResponse<CancelMicroflowRunResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MicroflowApiResponse<object>), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<MicroflowApiResponse<CancelMicroflowRunResponseDto>>> Cancel(
        string runId,
        CancellationToken cancellationToken)
    {
        var result = await _runtimeService.CancelAsync(runId, cancellationToken);
        return MicroflowOk(result);
    }

    private ActionResult<MicroflowApiResponse<object>> ServiceUnavailable(string message)
    {
        var traceId = TraceId;
        var error = new MicroflowApiError
        {
            Code = MicroflowApiErrorCode.MicroflowServiceUnavailable,
            Message = message,
            Retryable = false,
            HttpStatus = StatusCodes.Status503ServiceUnavailable,
            TraceId = traceId
        };
        return StatusCode(StatusCodes.Status503ServiceUnavailable, MicroflowApiResponse<object>.Fail(error, traceId));
    }
}
