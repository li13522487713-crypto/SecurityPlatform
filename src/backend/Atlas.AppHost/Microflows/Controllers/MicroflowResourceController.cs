using System.Diagnostics;
using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime.Actions;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Microflows.Controllers;

[Route("api/v1/microflows")]
public sealed class MicroflowResourceController : MicroflowApiControllerBase
{
    private readonly IMicroflowResourceService _resourceService;
    private readonly IMicroflowPublishService _publishService;
    private readonly IMicroflowVersionService _versionService;
    private readonly IMicroflowValidationService _validationService;
    private readonly IMicroflowReferenceService _referenceService;
    private readonly IMicroflowTestRunService _testRunService;
    private readonly IMicroflowExecutionPlanLoader _executionPlanLoader;
    private readonly IMicroflowFlowNavigator _flowNavigator;
    private readonly IMicroflowStorageDiagnosticsService _storageDiagnosticsService;
    private readonly IMicroflowActionExecutorRegistry _actionExecutorRegistry;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;

    public MicroflowResourceController(
        IMicroflowResourceService resourceService,
        IMicroflowPublishService publishService,
        IMicroflowVersionService versionService,
        IMicroflowValidationService validationService,
        IMicroflowReferenceService referenceService,
        IMicroflowTestRunService testRunService,
        IMicroflowExecutionPlanLoader executionPlanLoader,
        IMicroflowFlowNavigator flowNavigator,
        IMicroflowStorageDiagnosticsService storageDiagnosticsService,
        IMicroflowActionExecutorRegistry actionExecutorRegistry,
        IConfiguration configuration,
        IHostEnvironment environment,
        IMicroflowRequestContextAccessor requestContextAccessor)
        : base(requestContextAccessor)
    {
        _resourceService = resourceService;
        _publishService = publishService;
        _versionService = versionService;
        _validationService = validationService;
        _referenceService = referenceService;
        _testRunService = testRunService;
        _executionPlanLoader = executionPlanLoader;
        _flowNavigator = flowNavigator;
        _storageDiagnosticsService = storageDiagnosticsService;
        _actionExecutorRegistry = actionExecutorRegistry;
        _configuration = configuration;
        _environment = environment;
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

    [HttpPost("{id}/unpublish")]
    [ProducesResponseType(typeof(MicroflowApiResponse<MicroflowResourceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<MicroflowResourceDto>>> Unpublish(
        string id,
        [FromBody] UnpublishMicroflowRequestDto? request,
        CancellationToken cancellationToken)
    {
        var result = await _publishService.UnpublishAsync(id, request ?? new UnpublishMicroflowRequestDto(), cancellationToken);
        return MicroflowOk(result);
    }

    [HttpGet("{id}/impact")]
    [ProducesResponseType(typeof(MicroflowApiResponse<MicroflowPublishImpactAnalysisDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<MicroflowPublishImpactAnalysisDto>>> AnalyzeImpact(
        string id,
        [FromQuery] string? version,
        [FromQuery] bool includeBreakingChanges = true,
        [FromQuery] bool includeReferences = true,
        CancellationToken cancellationToken = default)
    {
        var result = await _publishService.AnalyzeImpactAsync(
            id,
            new AnalyzeMicroflowImpactRequestDto { Version = version, IncludeBreakingChanges = includeBreakingChanges, IncludeReferences = includeReferences },
            cancellationToken);
        return MicroflowOk(result);
    }

    [HttpGet("{id}/references")]
    [ProducesResponseType(typeof(MicroflowApiResponse<IReadOnlyList<MicroflowReferenceDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<IReadOnlyList<MicroflowReferenceDto>>>> GetReferences(
        string id,
        [FromQuery] bool includeInactive = false,
        [FromQuery] string[]? sourceType = null,
        [FromQuery] string[]? impactLevel = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _referenceService.GetReferencesAsync(
            id,
            new GetMicroflowReferencesRequestDto
            {
                IncludeInactive = includeInactive,
                SourceType = sourceType ?? Array.Empty<string>(),
                ImpactLevel = impactLevel ?? Array.Empty<string>()
            },
            cancellationToken);
        return MicroflowOk(result);
    }

    [HttpPost("{id}/references/rebuild")]
    [ProducesResponseType(typeof(MicroflowApiResponse<IReadOnlyList<MicroflowReferenceDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<IReadOnlyList<MicroflowReferenceDto>>>> RebuildReferences(
        string id,
        CancellationToken cancellationToken)
    {
        var result = await _referenceService.RebuildReferencesAsync(id, cancellationToken);
        return MicroflowOk(result);
    }

    [HttpGet("{id}/callers")]
    [ProducesResponseType(typeof(MicroflowApiResponse<IReadOnlyList<MicroflowReferenceDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<IReadOnlyList<MicroflowReferenceDto>>>> ListCallers(
        string id,
        [FromQuery] bool includeInactive = false,
        [FromQuery] string[]? sourceType = null,
        [FromQuery] string[]? impactLevel = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _referenceService.ListCallersAsync(
            id,
            new GetMicroflowReferencesRequestDto
            {
                IncludeInactive = includeInactive,
                SourceType = sourceType ?? Array.Empty<string>(),
                ImpactLevel = impactLevel ?? Array.Empty<string>()
            },
            cancellationToken);
        return MicroflowOk(result);
    }

    [HttpGet("{id}/callees")]
    [ProducesResponseType(typeof(MicroflowApiResponse<IReadOnlyList<MicroflowReferenceDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<IReadOnlyList<MicroflowReferenceDto>>>> ListCallees(
        string id,
        [FromQuery] bool includeInactive = false,
        [FromQuery] string[]? sourceType = null,
        [FromQuery] string[]? impactLevel = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _referenceService.ListCalleesAsync(
            id,
            new GetMicroflowReferencesRequestDto
            {
                IncludeInactive = includeInactive,
                SourceType = sourceType ?? Array.Empty<string>(),
                ImpactLevel = impactLevel ?? Array.Empty<string>()
            },
            cancellationToken);
        return MicroflowOk(result);
    }

    [HttpPost("runtime/plan")]
    [ProducesResponseType(typeof(MicroflowApiResponse<MicroflowExecutionPlan>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<MicroflowExecutionPlan>>> BuildRuntimePlan(
        [FromBody] LoadMicroflowExecutionPlanRequestDto request,
        CancellationToken cancellationToken)
    {
        var options = request.Options ?? new MicroflowExecutionPlanLoadOptions();
        var result = await _executionPlanLoader.LoadFromSchemaAsync(request.Schema, options, cancellationToken);
        return MicroflowOk(result);
    }

    [HttpPost("runtime/navigate")]
    [ProducesResponseType(typeof(MicroflowApiResponse<MicroflowNavigationResult>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<MicroflowNavigationResult>>> NavigateRuntimePlan(
        [FromBody] NavigateMicroflowRuntimeRequestDto request,
        CancellationToken cancellationToken)
    {
        var options = request.Options ?? new MicroflowNavigationOptions();
        var plan = await _executionPlanLoader.LoadFromSchemaAsync(
            request.Schema,
            new MicroflowExecutionPlanLoadOptions
            {
                Mode = MicroflowExecutionPlanMode.ValidateOnly,
                IncludeDiagnostics = options.IncludeDiagnostics,
                FailOnUnsupported = false
            },
            cancellationToken);
        var result = await _flowNavigator.NavigateAsync(plan, options, cancellationToken);
        return MicroflowOk(result);
    }

    [HttpGet("{id}/runtime/plan")]
    [ProducesResponseType(typeof(MicroflowApiResponse<MicroflowExecutionPlan>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<MicroflowExecutionPlan>>> GetCurrentRuntimePlan(
        string id,
        [FromQuery] string? mode = null,
        [FromQuery] bool failOnUnsupported = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _executionPlanLoader.LoadCurrentAsync(
            id,
            new MicroflowExecutionPlanLoadOptions
            {
                Mode = string.IsNullOrWhiteSpace(mode) ? MicroflowExecutionPlanMode.ValidateOnly : mode!,
                FailOnUnsupported = failOnUnsupported
            },
            cancellationToken);
        return MicroflowOk(result);
    }

    [HttpGet("{id}/runtime/navigate")]
    [ProducesResponseType(typeof(MicroflowApiResponse<MicroflowNavigationResult>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<MicroflowNavigationResult>>> NavigateCurrentRuntimePlan(
        string id,
        [FromQuery] string? mode = null,
        [FromQuery] int? maxSteps = null,
        [FromQuery] bool? decisionBooleanResult = null,
        [FromQuery] string? enumerationCaseValue = null,
        [FromQuery] string? objectTypeCase = null,
        [FromQuery] int? loopIterations = null,
        [FromQuery] bool simulateRestError = false,
        [FromQuery] bool? stopOnUnsupported = null,
        CancellationToken cancellationToken = default)
    {
        var options = new MicroflowNavigationOptions
        {
            Mode = string.IsNullOrWhiteSpace(mode) ? MicroflowNavigationMode.DryRun : mode!,
            MaxSteps = maxSteps,
            DecisionBooleanResult = decisionBooleanResult,
            EnumerationCaseValue = enumerationCaseValue,
            ObjectTypeCase = objectTypeCase,
            LoopIterations = loopIterations,
            SimulateRestError = simulateRestError,
            StopOnUnsupported = stopOnUnsupported,
            IncludeDiagnostics = true
        };
        var plan = await _executionPlanLoader.LoadCurrentAsync(
            id,
            new MicroflowExecutionPlanLoadOptions
            {
                Mode = MicroflowExecutionPlanMode.ValidateOnly,
                IncludeDiagnostics = true,
                FailOnUnsupported = false
            },
            cancellationToken);
        var result = await _flowNavigator.NavigateAsync(plan, options, cancellationToken);
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

    [HttpGet("{id}/versions/{versionId}/runtime/plan")]
    [ProducesResponseType(typeof(MicroflowApiResponse<MicroflowExecutionPlan>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<MicroflowExecutionPlan>>> GetVersionRuntimePlan(
        string id,
        string versionId,
        [FromQuery] string? mode = null,
        [FromQuery] bool failOnUnsupported = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _executionPlanLoader.LoadVersionAsync(
            id,
            versionId,
            new MicroflowExecutionPlanLoadOptions
            {
                Mode = string.IsNullOrWhiteSpace(mode) ? MicroflowExecutionPlanMode.ValidateOnly : mode!,
                FailOnUnsupported = failOnUnsupported
            },
            cancellationToken);
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
        var started = Stopwatch.GetTimestamp();
        var traceId = TraceId;
        return MicroflowOk(new MicroflowHealthDto
        {
            Status = "ok",
            Service = "microflows",
            Version = "round61-production-readiness",
            Timestamp = DateTimeOffset.UtcNow,
            TraceId = traceId,
            DurationMs = Stopwatch.GetElapsedTime(started).Milliseconds,
            Environment = _environment.EnvironmentName,
            Checks =
            [
                new MicroflowHealthCheckDto { Name = "process", Status = "healthy", Summary = "Microflow API process is alive." },
                new MicroflowHealthCheckDto { Name = "runtimeOptions", Status = "healthy", Summary = "Runtime safe defaults are loaded from code and configuration." }
            ]
        });
    }

    [HttpGet("runtime/health")]
    [ProducesResponseType(typeof(MicroflowApiResponse<MicroflowHealthDto>), StatusCodes.Status200OK)]
    public ActionResult<MicroflowApiResponse<MicroflowHealthDto>> GetRuntimeHealth()
    {
        var started = Stopwatch.GetTimestamp();
        var traceId = TraceId;
        var descriptorCount = _actionExecutorRegistry.ListAll().Count;
        var maxSteps = _configuration.GetValue("Microflow:Runtime:MaxSteps", 5000);
        var runTimeout = _configuration.GetValue("Microflow:Runtime:RunTimeoutSeconds", 300);
        var allowRealHttp = _configuration.GetValue("Microflow:Runtime:Rest:AllowRealHttp", false);
        var allowPrivateNetwork = _configuration.GetValue("Microflow:Runtime:Rest:AllowPrivateNetwork", false);
        var checks = new List<MicroflowHealthCheckDto>
        {
            new() { Name = "actionExecutorRegistry", Status = descriptorCount > 0 ? "healthy" : "unhealthy", Summary = $"{descriptorCount} action descriptors registered." },
            new() { Name = "runtimeLimits", Status = maxSteps > 0 && runTimeout > 0 ? "healthy" : "unhealthy", Summary = $"MaxSteps={maxSteps}; RunTimeoutSeconds={runTimeout}." },
            new() { Name = "restSecurity", Status = !allowRealHttp && !allowPrivateNetwork ? "healthy" : "degraded", Summary = $"AllowRealHttp={allowRealHttp}; AllowPrivateNetwork={allowPrivateNetwork}." }
        };
        var status = checks.Any(static check => check.Status == "unhealthy")
            ? "unhealthy"
            : checks.Any(static check => check.Status == "degraded")
                ? "degraded"
                : "healthy";

        return MicroflowOk(new MicroflowHealthDto
        {
            Status = status,
            Service = "microflows-runtime",
            Version = "round61-production-readiness",
            Timestamp = DateTimeOffset.UtcNow,
            TraceId = traceId,
            DurationMs = Stopwatch.GetElapsedTime(started).Milliseconds,
            Environment = _environment.EnvironmentName,
            Checks = checks
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
        [FromQuery] string? folderId = null,
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
                FolderId = folderId,
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
    [ProducesResponseType(typeof(MicroflowApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(MicroflowApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
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

    [HttpPost("{id}/move")]
    [ProducesResponseType(typeof(MicroflowApiResponse<MicroflowResourceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<MicroflowResourceDto>>> Move(
        string id,
        [FromBody] MoveMicroflowRequestDto request,
        CancellationToken cancellationToken)
    {
        var resource = await _resourceService.MoveAsync(id, request, cancellationToken);
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
    [ProducesResponseType(typeof(MicroflowApiResponse<TestRunMicroflowApiResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MicroflowApiResponse<TestRunMicroflowApiResponse>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MicroflowApiResponse<TestRunMicroflowApiResponse>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(MicroflowApiResponse<TestRunMicroflowApiResponse>), StatusCodes.Status408RequestTimeout)]
    [ProducesResponseType(typeof(MicroflowApiResponse<TestRunMicroflowApiResponse>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<MicroflowApiResponse<TestRunMicroflowApiResponse>>> TestRun(
        string id,
        [FromBody] TestRunMicroflowApiRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _testRunService.TestRunAsync(id, request, cancellationToken);
        var httpStatus = RuntimeHttpStatus(result.ErrorCode);
        if (httpStatus != StatusCodes.Status200OK)
        {
            return StatusCode(httpStatus, MicroflowApiResponse<TestRunMicroflowApiResponse>.Ok(result, TraceId));
        }

        return MicroflowOk(result);
    }

    private static int RuntimeHttpStatus(string? errorCode)
    {
        return errorCode switch
        {
            null or "" => StatusCodes.Status200OK,
            RuntimeErrorCode.RuntimeTimeout or RuntimeErrorCode.RuntimeRestTimeout => StatusCodes.Status408RequestTimeout,
            RuntimeErrorCode.RuntimeEntityAccessDenied => StatusCodes.Status403Forbidden,
            RuntimeErrorCode.RuntimeTargetMicroflowNotFound or RuntimeErrorCode.RuntimeObjectNotFound => StatusCodes.Status404NotFound,
            RuntimeErrorCode.RuntimeStartNotFound
                or RuntimeErrorCode.RuntimeFlowNotFound
                or RuntimeErrorCode.RuntimeInvalidCase
                or RuntimeErrorCode.RuntimeExpressionError
                or RuntimeErrorCode.RuntimeParameterMappingMissing
                or RuntimeErrorCode.RuntimeParameterMappingFailed
                or RuntimeErrorCode.RuntimeReturnBindingFailed
                or RuntimeErrorCode.RuntimeUnsupportedAction
                or RuntimeErrorCode.RuntimeConnectorRequired
                or RuntimeErrorCode.RuntimeLoopSourceNotFound
                or RuntimeErrorCode.RuntimeLoopSourceNotList
                or RuntimeErrorCode.RuntimeLoopIteratorInvalid
                or RuntimeErrorCode.RuntimeLoopConditionError
                or RuntimeErrorCode.RuntimeLoopConditionNotBoolean
                or RuntimeErrorCode.RuntimeLoopControlOutOfScope => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status200OK
        };
    }

    [HttpGet("runs/{runId}")]
    [ProducesResponseType(typeof(MicroflowApiResponse<MicroflowRunSessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MicroflowApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MicroflowApiResponse<MicroflowRunSessionDto>>> GetRun(
        string runId,
        CancellationToken cancellationToken)
    {
        var result = await _testRunService.GetRunSessionAsync(runId, cancellationToken);
        return MicroflowOk(result);
    }

    [HttpGet("{id}/runs")]
    [ProducesResponseType(typeof(MicroflowApiResponse<ListMicroflowRunsResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MicroflowApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MicroflowApiResponse<ListMicroflowRunsResponse>>> ListRuns(
        string id,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _testRunService.ListRunsAsync(
            id,
            new ListMicroflowRunsRequest
            {
                PageIndex = pageIndex,
                PageSize = pageSize,
                Status = status
            },
            cancellationToken);
        return MicroflowOk(result);
    }

    [HttpGet("{id}/runs/{runId}")]
    [ProducesResponseType(typeof(MicroflowApiResponse<MicroflowRunSessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MicroflowApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MicroflowApiResponse<MicroflowRunSessionDto>>> GetRunByMicroflow(
        string id,
        string runId,
        CancellationToken cancellationToken)
    {
        var result = await _testRunService.GetRunSessionAsync(id, runId, cancellationToken);
        return MicroflowOk(result);
    }

    [HttpGet("runs/{runId}/trace")]
    [ProducesResponseType(typeof(MicroflowApiResponse<GetMicroflowRunTraceResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MicroflowApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MicroflowApiResponse<GetMicroflowRunTraceResponse>>> GetTrace(
        string runId,
        CancellationToken cancellationToken)
    {
        var result = await _testRunService.GetRunTraceAsync(runId, cancellationToken);
        return MicroflowOk(result);
    }

    [HttpPost("runs/{runId}/cancel")]
    [ProducesResponseType(typeof(MicroflowApiResponse<CancelMicroflowRunResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MicroflowApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MicroflowApiResponse<CancelMicroflowRunResponse>>> Cancel(
        string runId,
        CancellationToken cancellationToken)
    {
        var result = await _testRunService.CancelAsync(runId, cancellationToken);
        return MicroflowOk(result);
    }
}
