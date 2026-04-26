using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Abstractions.Knowledge;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

/// <summary>
/// 知识库专题（v5 §32-44）扩展接口（AppHost 权威版）：提供 jobs / bindings / permissions /
/// versions / retrieval-logs / provider-configs / table / image API，便于 AppHost 内部 Workflow / Agent / App 调用。
/// </summary>
[ApiController]
[Route("api/v1/knowledge-bases")]
public sealed class KnowledgeBasesV5Controller : ControllerBase
{
    private readonly IKnowledgeJobService _jobService;
    private readonly IKnowledgeParseJobService _parseJobService;
    private readonly IKnowledgeIndexJobService _indexJobService;
    private readonly IKnowledgeBindingService _bindingService;
    private readonly IKnowledgePermissionService _permissionService;
    private readonly IKnowledgeVersionService _versionService;
    private readonly IRetrievalLogService _retrievalLogService;
    private readonly IKnowledgeProviderConfigService _providerConfigService;
    private readonly IKnowledgeTableViewService _tableViewService;
    private readonly IKnowledgeImageItemService _imageItemService;
    private readonly IRagRetrievalService _ragRetrievalService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IValidator<KnowledgeBindingCreateRequest> _bindingValidator;
    private readonly IValidator<KnowledgePermissionGrantRequest> _permissionValidator;
    private readonly IValidator<KnowledgePermissionUpdateRequest> _permissionUpdateValidator;
    private readonly IValidator<KnowledgeVersionCreateRequest> _versionValidator;
    private readonly IValidator<RerunParseRequest> _rerunValidator;
    private readonly IValidator<RebuildIndexRequest> _rebuildValidator;
    private readonly IValidator<ParseJobReplayRequest> _parseJobReplayValidator;
    private readonly IValidator<IndexJobRebuildRequest> _indexJobRebuildValidator;
    private readonly IValidator<DeadLetterRetryRequest> _deadLetterRetryValidator;
    private readonly IValidator<KnowledgeProviderConfigUpsertRequest> _providerUpsertValidator;
    private readonly IValidator<RetrievalRequest> _retrievalRequestValidator;

    public KnowledgeBasesV5Controller(
        IKnowledgeJobService jobService,
        IKnowledgeParseJobService parseJobService,
        IKnowledgeIndexJobService indexJobService,
        IKnowledgeBindingService bindingService,
        IKnowledgePermissionService permissionService,
        IKnowledgeVersionService versionService,
        IRetrievalLogService retrievalLogService,
        IKnowledgeProviderConfigService providerConfigService,
        IKnowledgeTableViewService tableViewService,
        IKnowledgeImageItemService imageItemService,
        IRagRetrievalService ragRetrievalService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IValidator<KnowledgeBindingCreateRequest> bindingValidator,
        IValidator<KnowledgePermissionGrantRequest> permissionValidator,
        IValidator<KnowledgePermissionUpdateRequest> permissionUpdateValidator,
        IValidator<KnowledgeVersionCreateRequest> versionValidator,
        IValidator<RerunParseRequest> rerunValidator,
        IValidator<RebuildIndexRequest> rebuildValidator,
        IValidator<ParseJobReplayRequest> parseJobReplayValidator,
        IValidator<IndexJobRebuildRequest> indexJobRebuildValidator,
        IValidator<DeadLetterRetryRequest> deadLetterRetryValidator,
        IValidator<KnowledgeProviderConfigUpsertRequest> providerUpsertValidator,
        IValidator<RetrievalRequest> retrievalRequestValidator)
    {
        _jobService = jobService;
        _parseJobService = parseJobService;
        _indexJobService = indexJobService;
        _bindingService = bindingService;
        _permissionService = permissionService;
        _versionService = versionService;
        _retrievalLogService = retrievalLogService;
        _providerConfigService = providerConfigService;
        _tableViewService = tableViewService;
        _imageItemService = imageItemService;
        _ragRetrievalService = ragRetrievalService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _bindingValidator = bindingValidator;
        _permissionValidator = permissionValidator;
        _permissionUpdateValidator = permissionUpdateValidator;
        _versionValidator = versionValidator;
        _rerunValidator = rerunValidator;
        _rebuildValidator = rebuildValidator;
        _parseJobReplayValidator = parseJobReplayValidator;
        _indexJobRebuildValidator = indexJobRebuildValidator;
        _deadLetterRetryValidator = deadLetterRetryValidator;
        _providerUpsertValidator = providerUpsertValidator;
        _retrievalRequestValidator = retrievalRequestValidator;
    }

    /* -------------------- jobs -------------------- */

    [HttpGet("{id:long}/jobs")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseView)]
    public async Task<ActionResult<ApiResponse<PagedResult<KnowledgeJobDto>>>> ListJobs(
        long id,
        [FromQuery] KnowledgeJobsListRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _jobService.ListAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<PagedResult<KnowledgeJobDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("jobs")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseView)]
    public async Task<ActionResult<ApiResponse<PagedResult<KnowledgeJobDto>>>> ListAllJobs(
        [FromQuery] KnowledgeJobsListRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _jobService.ListAcrossKnowledgeBasesAsync(tenantId, request, cancellationToken);
        return Ok(ApiResponse<PagedResult<KnowledgeJobDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/jobs/{jobId:long}")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseView)]
    public async Task<ActionResult<ApiResponse<KnowledgeJobDto>>> GetJob(
        long id,
        long jobId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _jobService.GetAsync(tenantId, id, jobId, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<KnowledgeJobDto>.Fail(ErrorCodes.NotFound, "Knowledge job not found", HttpContext.TraceIdentifier));
        }
        return Ok(ApiResponse<KnowledgeJobDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/jobs/parse")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> RerunParse(
        long id,
        [FromBody] RerunParseRequest request,
        CancellationToken cancellationToken)
    {
        _rerunValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var jobId = await _jobService.RerunParseAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = jobId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/jobs/rebuild-index")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> RebuildIndex(
        long id,
        [FromBody] RebuildIndexRequest? request,
        CancellationToken cancellationToken)
    {
        var effective = request ?? new RebuildIndexRequest();
        _rebuildValidator.ValidateAndThrow(effective);
        var tenantId = _tenantProvider.GetTenantId();
        var jobId = await _jobService.RebuildIndexAsync(tenantId, id, effective, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = jobId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/jobs/{jobId:long}:retry")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> RetryJob(
        long id,
        long jobId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _jobService.RetryDeadLetterAsync(tenantId, id, jobId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = jobId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/jobs/{jobId:long}:cancel")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> CancelJob(
        long id,
        long jobId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _jobService.CancelAsync(tenantId, id, jobId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = jobId.ToString() }, HttpContext.TraceIdentifier));
    }

    /* -------------------- v5 §35 / 计划 G5：document 范围的 parse / index jobs -------------------- */

    [HttpGet("{id:long}/documents/{docId:long}/parse-jobs")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ParseJobDto>>>> ListDocumentParseJobs(
        long id,
        long docId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _parseJobService.ListByDocumentAsync(tenantId, id, docId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ParseJobDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/documents/{docId:long}/parse-jobs")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> ReplayDocumentParseJob(
        long id,
        long docId,
        [FromBody] ParseJobReplayRequest? request,
        CancellationToken cancellationToken)
    {
        var effective = request ?? new ParseJobReplayRequest();
        _parseJobReplayValidator.ValidateAndThrow(effective);
        var tenantId = _tenantProvider.GetTenantId();
        var jobId = await _parseJobService.ReplayAsync(tenantId, id, docId, effective, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = jobId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/documents/{docId:long}/index-jobs")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<IndexJobDto>>>> ListDocumentIndexJobs(
        long id,
        long docId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _indexJobService.ListByDocumentAsync(tenantId, id, docId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<IndexJobDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/documents/{docId:long}/index-jobs/rebuild")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> RebuildDocumentIndex(
        long id,
        long docId,
        [FromBody] IndexJobRebuildRequest? request,
        CancellationToken cancellationToken)
    {
        var effective = request ?? new IndexJobRebuildRequest();
        _indexJobRebuildValidator.ValidateAndThrow(effective);
        var tenantId = _tenantProvider.GetTenantId();
        var jobId = await _indexJobService.RebuildAsync(tenantId, id, docId, effective, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = jobId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/jobs/dead-letter:retry")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> RetryDeadLetterBatch(
        long id,
        [FromBody] DeadLetterRetryRequest? request,
        CancellationToken cancellationToken)
    {
        var effective = request ?? new DeadLetterRetryRequest();
        _deadLetterRetryValidator.ValidateAndThrow(effective);
        var tenantId = _tenantProvider.GetTenantId();
        var count = await _jobService.RetryDeadLetterBatchAsync(tenantId, id, effective, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { RetriedCount = count }, HttpContext.TraceIdentifier));
    }

    /* -------------------- bindings -------------------- */

    [HttpGet("{id:long}/bindings")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseView)]
    public async Task<ActionResult<ApiResponse<PagedResult<KnowledgeBindingDto>>>> ListBindings(
        long id,
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _bindingService.ListAsync(tenantId, id, request.PageIndex, request.PageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<KnowledgeBindingDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("bindings")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseView)]
    public async Task<ActionResult<ApiResponse<PagedResult<KnowledgeBindingDto>>>> ListAllBindings(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _bindingService.ListAllAsync(tenantId, request.PageIndex, request.PageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<KnowledgeBindingDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/bindings")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> CreateBinding(
        long id,
        [FromBody] KnowledgeBindingCreateRequest request,
        CancellationToken cancellationToken)
    {
        _bindingValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var bindingId = await _bindingService.CreateAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = bindingId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/bindings/{bindingId:long}")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseView)]
    public async Task<ActionResult<ApiResponse<KnowledgeBindingDto>>> GetBinding(
        long id,
        long bindingId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var dto = await _bindingService.GetByIdAsync(tenantId, id, bindingId, cancellationToken);
        if (dto is null)
        {
            return NotFound(ApiResponse<KnowledgeBindingDto>.Fail(ErrorCodes.NotFound, "Binding not found", HttpContext.TraceIdentifier));
        }
        return Ok(ApiResponse<KnowledgeBindingDto>.Ok(dto, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}/bindings/{bindingId:long}")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> RemoveBinding(
        long id,
        long bindingId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _bindingService.RemoveAsync(tenantId, id, bindingId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = bindingId.ToString() }, HttpContext.TraceIdentifier));
    }

    /* -------------------- permissions -------------------- */

    [HttpGet("{id:long}/permissions")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseView)]
    public async Task<ActionResult<ApiResponse<PagedResult<KnowledgePermissionDto>>>> ListPermissions(
        long id,
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _permissionService.ListAsync(tenantId, id, request.PageIndex, request.PageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<KnowledgePermissionDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/permissions")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> GrantPermission(
        long id,
        [FromBody] KnowledgePermissionGrantRequest request,
        CancellationToken cancellationToken)
    {
        _permissionValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUserAccessor.GetCurrentUserOrThrow();
        var permId = await _permissionService.GrantAsync(tenantId, id, request, user.UserId.ToString(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = permId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}/permissions/{permissionId:long}")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> UpdatePermission(
        long id,
        long permissionId,
        [FromBody] KnowledgePermissionUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _permissionUpdateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _permissionService.UpdateAsync(tenantId, id, permissionId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = permissionId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}/permissions/{permissionId:long}")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> RevokePermission(
        long id,
        long permissionId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _permissionService.RevokeAsync(tenantId, id, permissionId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = permissionId.ToString() }, HttpContext.TraceIdentifier));
    }

    /* -------------------- versions -------------------- */

    [HttpGet("{id:long}/versions")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseView)]
    public async Task<ActionResult<ApiResponse<PagedResult<KnowledgeVersionDto>>>> ListVersions(
        long id,
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _versionService.ListAsync(tenantId, id, request.PageIndex, request.PageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<KnowledgeVersionDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/versions")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> CreateVersion(
        long id,
        [FromBody] KnowledgeVersionCreateRequest request,
        CancellationToken cancellationToken)
    {
        _versionValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUserAccessor.GetCurrentUserOrThrow();
        var versionId = await _versionService.CreateSnapshotAsync(tenantId, id, request, user.UserId.ToString(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = versionId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/versions/{versionId:long}:release")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> ReleaseVersion(
        long id,
        long versionId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _versionService.ReleaseAsync(tenantId, id, versionId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = versionId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/versions/{versionId:long}:rollback")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> RollbackVersion(
        long id,
        long versionId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _versionService.RollbackAsync(tenantId, id, versionId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = versionId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/versions/diff")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseView)]
    public async Task<ActionResult<ApiResponse<KnowledgeVersionDiffDto>>> DiffVersions(
        long id,
        [FromQuery] long from,
        [FromQuery] long to,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var diff = await _versionService.DiffAsync(tenantId, id, from, to, cancellationToken);
        return Ok(ApiResponse<KnowledgeVersionDiffDto>.Ok(diff, HttpContext.TraceIdentifier));
    }

    /* -------------------- retrieval-logs / retrieval -------------------- */

    [HttpGet("{id:long}/retrieval-logs")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseView)]
    public async Task<ActionResult<ApiResponse<PagedResult<RetrievalLogDto>>>> ListRetrievalLogs(
        long id,
        [FromQuery] RetrievalLogQuery query,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _retrievalLogService.ListAsync(tenantId, id, query, cancellationToken);
        return Ok(ApiResponse<PagedResult<RetrievalLogDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("retrieval-logs/{traceId}")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseView)]
    public async Task<ActionResult<ApiResponse<RetrievalLogDto>>> GetRetrievalLog(
        string traceId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var log = await _retrievalLogService.GetAsync(tenantId, traceId, cancellationToken);
        if (log is null)
        {
            return NotFound(ApiResponse<RetrievalLogDto>.Fail(ErrorCodes.NotFound, "Retrieval log not found", HttpContext.TraceIdentifier));
        }
        return Ok(ApiResponse<RetrievalLogDto>.Ok(log, HttpContext.TraceIdentifier));
    }

    [HttpPost("retrieval")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseView)]
    public async Task<ActionResult<ApiResponse<RetrievalResponseDto>>> Retrieve(
        [FromBody] RetrievalRequest request,
        CancellationToken cancellationToken)
    {
        _retrievalRequestValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _ragRetrievalService.SearchWithProfileAsync(tenantId, request, cancellationToken);
        return Ok(ApiResponse<RetrievalResponseDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    /* -------------------- table / image views -------------------- */

    [HttpGet("{id:long}/documents/{docId:long}/table-columns")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<KnowledgeTableColumnDto>>>> ListTableColumns(
        long id,
        long docId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _tableViewService.ListColumnsAsync(tenantId, id, docId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<KnowledgeTableColumnDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/documents/{docId:long}/table-rows")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseView)]
    public async Task<ActionResult<ApiResponse<PagedResult<KnowledgeTableRowDto>>>> ListTableRows(
        long id,
        long docId,
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _tableViewService.ListRowsAsync(tenantId, id, docId, request.PageIndex, request.PageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<KnowledgeTableRowDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/documents/{docId:long}/image-items")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseView)]
    public async Task<ActionResult<ApiResponse<PagedResult<KnowledgeImageItemDto>>>> ListImageItems(
        long id,
        long docId,
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _imageItemService.ListAsync(tenantId, id, docId, request.PageIndex, request.PageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<KnowledgeImageItemDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    /* -------------------- provider configs -------------------- */

    [HttpGet("provider-configs")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<KnowledgeProviderConfigDto>>>> ListProviderConfigs(
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _providerConfigService.ListAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<KnowledgeProviderConfigDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// v5 §39 / 计划 G5：admin 通过 PUT /provider-configs/{role} 写入或更新该 role 的默认 provider。
    /// 路径参数 role 必须与 body 的 role 一致；不一致返回 400。
    /// </summary>
    [HttpPut("provider-configs/{role}")]
    [Authorize(Policy = PermissionPolicies.KnowledgeBaseUpdate)]
    public async Task<ActionResult<ApiResponse<KnowledgeProviderConfigDto>>> UpsertProviderConfig(
        string role,
        [FromBody] KnowledgeProviderConfigUpsertRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<KnowledgeProviderRole>(role, ignoreCase: true, out var parsedRole))
        {
            return BadRequest(ApiResponse<KnowledgeProviderConfigDto>.Fail(
                ErrorCodes.ValidationError,
                $"Unknown provider role '{role}'.",
                HttpContext.TraceIdentifier));
        }
        if (parsedRole != request.Role)
        {
            return BadRequest(ApiResponse<KnowledgeProviderConfigDto>.Fail(
                ErrorCodes.ValidationError,
                "Path role and body role mismatch.",
                HttpContext.TraceIdentifier));
        }
        _providerUpsertValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var dto = await _providerConfigService.UpsertAsync(tenantId, request, cancellationToken);
        return Ok(ApiResponse<KnowledgeProviderConfigDto>.Ok(dto, HttpContext.TraceIdentifier));
    }
}
