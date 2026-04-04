using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.PlatformHost.Controllers;

[ApiController]
[Route("api/v1/evaluations")]
[Authorize]
public sealed class EvaluationsController : ControllerBase
{
    private readonly IEvaluationService _evaluationService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IValidator<EvaluationDatasetCreateRequest> _datasetValidator;
    private readonly IValidator<EvaluationCaseCreateRequest> _caseValidator;
    private readonly IValidator<EvaluationTaskCreateRequest> _taskValidator;

    public EvaluationsController(
        IEvaluationService evaluationService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IValidator<EvaluationDatasetCreateRequest> datasetValidator,
        IValidator<EvaluationCaseCreateRequest> caseValidator,
        IValidator<EvaluationTaskCreateRequest> taskValidator)
    {
        _evaluationService = evaluationService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _datasetValidator = datasetValidator;
        _caseValidator = caseValidator;
        _taskValidator = taskValidator;
    }

    [HttpPost("datasets")]
    [Authorize(Policy = PermissionPolicies.AgentCreate)]
    public async Task<ActionResult<ApiResponse<object>>> CreateDataset(
        [FromBody] EvaluationDatasetCreateRequest request,
        CancellationToken cancellationToken)
    {
        _datasetValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var id = await _evaluationService.CreateDatasetAsync(tenantId, userId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpGet("datasets")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<PagedResult<EvaluationDatasetDto>>>> GetDatasets(
        [FromQuery] PagedRequest request,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _evaluationService.GetDatasetsAsync(
            tenantId,
            keyword,
            request.PageIndex,
            request.PageSize,
            cancellationToken);
        return Ok(ApiResponse<PagedResult<EvaluationDatasetDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("datasets/{datasetId:long}/cases")]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> CreateCase(
        long datasetId,
        [FromBody] EvaluationCaseCreateRequest request,
        CancellationToken cancellationToken)
    {
        _caseValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var id = await _evaluationService.CreateCaseAsync(tenantId, datasetId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpGet("datasets/{datasetId:long}/cases")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<EvaluationCaseDto>>>> GetCases(
        long datasetId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _evaluationService.GetCasesAsync(tenantId, datasetId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<EvaluationCaseDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("tasks")]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> CreateTask(
        [FromBody] EvaluationTaskCreateRequest request,
        CancellationToken cancellationToken)
    {
        _taskValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var id = await _evaluationService.CreateTaskAsync(tenantId, userId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpGet("tasks")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<PagedResult<EvaluationTaskDto>>>> GetTasks(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _evaluationService.GetTasksAsync(
            tenantId,
            request.PageIndex,
            request.PageSize,
            cancellationToken);
        return Ok(ApiResponse<PagedResult<EvaluationTaskDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("tasks/{taskId:long}")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<EvaluationTaskDto>>> GetTask(
        long taskId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _evaluationService.GetTaskAsync(tenantId, taskId, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<EvaluationTaskDto>.Fail(
                ErrorCodes.NotFound,
                ApiResponseLocalizer.T(HttpContext, "RecordNotFound"),
                HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<EvaluationTaskDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("tasks/{taskId:long}/results")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<EvaluationResultDto>>>> GetTaskResults(
        long taskId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _evaluationService.GetTaskResultsAsync(tenantId, taskId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<EvaluationResultDto>>.Ok(result, HttpContext.TraceIdentifier));
    }
}
