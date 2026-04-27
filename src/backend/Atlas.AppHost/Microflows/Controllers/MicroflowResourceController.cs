using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Microflows.Controllers;

[Route("api/microflows")]
[AllowAnonymous]
public sealed class MicroflowResourceController : MicroflowApiControllerBase
{
    private readonly IMicroflowResourceService _resourceService;
    private readonly IMicroflowPublishService _publishService;
    private readonly IMicroflowVersionService _versionService;
    private readonly IMicroflowValidationService _validationService;
    private readonly IMicroflowRuntimeSkeletonService _runtimeService;
    private readonly IMicroflowStorageDiagnosticsService _storageDiagnosticsService;

    public MicroflowResourceController(
        IMicroflowResourceService resourceService,
        IMicroflowPublishService publishService,
        IMicroflowVersionService versionService,
        IMicroflowValidationService validationService,
        IMicroflowRuntimeSkeletonService runtimeService,
        IMicroflowStorageDiagnosticsService storageDiagnosticsService,
        IMicroflowRequestContextAccessor requestContextAccessor)
        : base(requestContextAccessor)
    {
        _resourceService = resourceService;
        _publishService = publishService;
        _versionService = versionService;
        _validationService = validationService;
        _runtimeService = runtimeService;
        _storageDiagnosticsService = storageDiagnosticsService;
    }

    [HttpPost("{id}/publish")]
    [ProducesResponseType(typeof(MicroflowApiResponse<MicroflowPublishResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<MicroflowPublishResultDto>>> Publish(
        string id,
        [FromBody] PublishMicroflowApiRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _publishService.PublishAsync(id, request, cancellationToken);
        return MicroflowOk(result);
    }

    [HttpGet("{id}/impact")]
    [ProducesResponseType(typeof(MicroflowApiResponse<MicroflowPublishImpactAnalysisDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<MicroflowPublishImpactAnalysisDto>>> AnalyzeImpact(
        string id,
        [FromQuery] string? version,
        [FromQuery] bool includeBreakingChanges = true,
        CancellationToken cancellationToken = default)
    {
        var result = await _publishService.AnalyzeImpactAsync(
            id,
            new AnalyzeMicroflowImpactRequestDto { Version = version, IncludeBreakingChanges = includeBreakingChanges },
            cancellationToken);
        return MicroflowOk(result);
    }

    [HttpGet("{id}/versions")]
    [ProducesResponseType(typeof(MicroflowApiResponse<IReadOnlyList<MicroflowVersionSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<IReadOnlyList<MicroflowVersionSummaryDto>>>> ListVersions(
        string id,
        CancellationToken cancellationToken)
    {
        var result = await _versionService.ListVersionsAsync(id, cancellationToken);
        return MicroflowOk(result);
    }

    [HttpGet("{id}/versions/{versionId}")]
    [ProducesResponseType(typeof(MicroflowApiResponse<MicroflowVersionDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<MicroflowVersionDetailDto>>> GetVersionDetail(
        string id,
        string versionId,
        CancellationToken cancellationToken)
    {
        var result = await _versionService.GetVersionDetailAsync(id, versionId, cancellationToken);
        return MicroflowOk(result);
    }

    [HttpPost("{id}/versions/{versionId}/rollback")]
    [ProducesResponseType(typeof(MicroflowApiResponse<MicroflowResourceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<MicroflowResourceDto>>> RollbackVersion(
        string id,
        string versionId,
        [FromBody] RollbackMicroflowVersionRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _versionService.RollbackAsync(id, versionId, request, cancellationToken);
        return MicroflowOk(result);
    }

    [HttpPost("{id}/versions/{versionId}/duplicate")]
    [ProducesResponseType(typeof(MicroflowApiResponse<MicroflowResourceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<MicroflowResourceDto>>> DuplicateVersion(
        string id,
        string versionId,
        [FromBody] DuplicateMicroflowVersionRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _versionService.DuplicateVersionAsync(id, versionId, request, cancellationToken);
        return MicroflowOk(result);
    }

    [HttpGet("{id}/versions/{versionId}/compare-current")]
    [ProducesResponseType(typeof(MicroflowApiResponse<MicroflowVersionDiffDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<MicroflowVersionDiffDto>>> CompareCurrent(
        string id,
        string versionId,
        CancellationToken cancellationToken)
    {
        var result = await _versionService.CompareCurrentAsync(id, versionId, cancellationToken);
        return MicroflowOk(result);
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
        [FromQuery] string? ownerId = null,
        [FromQuery] string? moduleId = null,
        [FromQuery] string[]? tags = null,
        [FromQuery] DateTimeOffset? updatedFrom = null,
        [FromQuery] DateTimeOffset? updatedTo = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = null,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _resourceService.ListAsync(
            new ListMicroflowsRequestDto
            {
                WorkspaceId = workspaceId,
                Keyword = keyword,
                Status = status ?? Array.Empty<string>(),
                PublishStatus = publishStatus ?? Array.Empty<string>(),
                FavoriteOnly = favoriteOnly,
                OwnerId = ownerId,
                ModuleId = moduleId,
                Tags = tags ?? Array.Empty<string>(),
                UpdatedFrom = updatedFrom,
                UpdatedTo = updatedTo,
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
        var resource = await _resourceService.GetAsync(id, cancellationToken);
        return MicroflowOk(resource);
    }

    [HttpPost]
    [ProducesResponseType(typeof(MicroflowApiResponse<MicroflowResourceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<MicroflowResourceDto>>> Create(
        [FromBody] CreateMicroflowRequestDto request,
        CancellationToken cancellationToken)
    {
        var resource = await _resourceService.CreateAsync(request, cancellationToken);
        return MicroflowOk(resource);
    }

    [HttpPatch("{id}")]
    [ProducesResponseType(typeof(MicroflowApiResponse<MicroflowResourceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<MicroflowResourceDto>>> Update(
        string id,
        [FromBody] UpdateMicroflowResourceRequestDto request,
        CancellationToken cancellationToken)
    {
        var resource = await _resourceService.UpdateAsync(id, request, cancellationToken);
        return MicroflowOk(resource);
    }

    [HttpGet("{id}/schema")]
    [ProducesResponseType(typeof(MicroflowApiResponse<GetMicroflowSchemaResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<GetMicroflowSchemaResponseDto>>> GetSchema(
        string id,
        CancellationToken cancellationToken)
    {
        var result = await _resourceService.GetSchemaAsync(id, cancellationToken);
        return MicroflowOk(result);
    }

    [HttpPut("{id}/schema")]
    [ProducesResponseType(typeof(MicroflowApiResponse<SaveMicroflowSchemaResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<SaveMicroflowSchemaResponseDto>>> SaveSchema(
        string id,
        [FromBody] SaveMicroflowSchemaRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _resourceService.SaveSchemaAsync(id, request, cancellationToken);
        return MicroflowOk(result);
    }

    [HttpPost("{id}/duplicate")]
    [ProducesResponseType(typeof(MicroflowApiResponse<MicroflowResourceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<MicroflowResourceDto>>> Duplicate(
        string id,
        [FromBody] DuplicateMicroflowRequestDto request,
        CancellationToken cancellationToken)
    {
        var resource = await _resourceService.DuplicateAsync(id, request, cancellationToken);
        return MicroflowOk(resource);
    }

    [HttpPost("{id}/rename")]
    [ProducesResponseType(typeof(MicroflowApiResponse<MicroflowResourceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<MicroflowResourceDto>>> Rename(
        string id,
        [FromBody] RenameMicroflowRequestDto request,
        CancellationToken cancellationToken)
    {
        var resource = await _resourceService.RenameAsync(id, request, cancellationToken);
        return MicroflowOk(resource);
    }

    [HttpPost("{id}/favorite")]
    [ProducesResponseType(typeof(MicroflowApiResponse<MicroflowResourceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<MicroflowResourceDto>>> ToggleFavorite(
        string id,
        [FromBody] ToggleFavoriteMicroflowRequestDto request,
        CancellationToken cancellationToken)
    {
        var resource = await _resourceService.ToggleFavoriteAsync(id, request, cancellationToken);
        return MicroflowOk(resource);
    }

    [HttpPost("{id}/archive")]
    [ProducesResponseType(typeof(MicroflowApiResponse<MicroflowResourceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<MicroflowResourceDto>>> Archive(
        string id,
        CancellationToken cancellationToken)
    {
        var resource = await _resourceService.ArchiveAsync(id, cancellationToken);
        return MicroflowOk(resource);
    }

    [HttpPost("{id}/restore")]
    [ProducesResponseType(typeof(MicroflowApiResponse<MicroflowResourceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<MicroflowResourceDto>>> Restore(
        string id,
        CancellationToken cancellationToken)
    {
        var resource = await _resourceService.RestoreAsync(id, cancellationToken);
        return MicroflowOk(resource);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(MicroflowApiResponse<DeleteMicroflowResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<DeleteMicroflowResponseDto>>> Delete(
        string id,
        CancellationToken cancellationToken)
    {
        await _resourceService.DeleteAsync(id, cancellationToken);
        return MicroflowOk(new DeleteMicroflowResponseDto { Id = id });
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
}
