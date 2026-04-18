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
/// 知识库专题（v5 §32-44）扩展接口（AppHost 镜像版）：与 PlatformHost 保持完全对称，
/// 共用同一组应用服务实现，便于 AppHost 内部 Workflow / Agent / App 调用知识库治理接口。
/// </summary>
[ApiController]
[Route("api/v1/knowledge-bases")]
public sealed class KnowledgeBasesV5Controller : ControllerBase
{
    private readonly IKnowledgeJobService _jobService;
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
    private readonly IValidator<KnowledgeVersionCreateRequest> _versionValidator;
    private readonly IValidator<RerunParseRequest> _rerunValidator;
    private readonly IValidator<RebuildIndexRequest> _rebuildValidator;
    private readonly IValidator<RetrievalRequest> _retrievalRequestValidator;

    public KnowledgeBasesV5Controller(
        IKnowledgeJobService jobService,
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
        IValidator<KnowledgeVersionCreateRequest> versionValidator,
        IValidator<RerunParseRequest> rerunValidator,
        IValidator<RebuildIndexRequest> rebuildValidator,
        IValidator<RetrievalRequest> retrievalRequestValidator)
    {
        _jobService = jobService;
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
        _versionValidator = versionValidator;
        _rerunValidator = rerunValidator;
        _rebuildValidator = rebuildValidator;
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
}
