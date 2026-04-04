using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Atlas.Presentation.Shared.Helpers;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers;

[ApiController]
[Route("api/v1/ai-workflows")]
public sealed class AiWorkflowsController : ControllerBase
{
    private readonly IAiWorkflowDesignService _designService;
    private readonly IAiWorkflowExecutionService _executionService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IValidator<AiWorkflowCreateRequest> _createValidator;
    private readonly IValidator<AiWorkflowSaveRequest> _saveValidator;
    private readonly IValidator<AiWorkflowMetaUpdateRequest> _metaValidator;

    public AiWorkflowsController(
        IAiWorkflowDesignService designService,
        IAiWorkflowExecutionService executionService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IValidator<AiWorkflowCreateRequest> createValidator,
        IValidator<AiWorkflowSaveRequest> saveValidator,
        IValidator<AiWorkflowMetaUpdateRequest> metaValidator)
    {
        _designService = designService;
        _executionService = executionService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _createValidator = createValidator;
        _saveValidator = saveValidator;
        _metaValidator = metaValidator;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<PagedResult<AiWorkflowDefinitionDto>>>> GetPaged(
        [FromQuery] PagedRequest request,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _designService.ListAsync(tenantId, keyword, request.PageIndex, request.PageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<AiWorkflowDefinitionDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<AiWorkflowDetailDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _designService.GetByIdAsync(tenantId, id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<AiWorkflowDetailDto>.Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "WorkflowDefNotFound"), HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<AiWorkflowDetailDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AiWorkflowCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] AiWorkflowCreateRequest request,
        CancellationToken cancellationToken)
    {
        _createValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var id = await _designService.CreateAsync(tenantId, userId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Save(
        long id,
        [FromBody] AiWorkflowSaveRequest request,
        CancellationToken cancellationToken)
    {
        _saveValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _designService.SaveAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}/meta")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateMeta(
        long id,
        [FromBody] AiWorkflowMetaUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _metaValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _designService.UpdateMetaAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _designService.DeleteAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/copy")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Copy(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var copiedId = await _designService.CopyAsync(tenantId, userId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = copiedId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/publish")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Publish(long id, CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var tenantId = _tenantProvider.GetTenantId();
        await _designService.PublishAsync(tenantId, currentUser.UserId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/validate")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowUpdate)]
    public async Task<ActionResult<ApiResponse<AiWorkflowValidateResult>>> Validate(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _designService.ValidateAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<AiWorkflowValidateResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/run")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowExecute)]
    public async Task<ActionResult<ApiResponse<AiWorkflowExecutionRunResult>>> Run(
        long id,
        [FromBody] AiWorkflowExecutionRunRequest? request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var safeRequest = request ?? new AiWorkflowExecutionRunRequest(new Dictionary<string, object?>());
        var result = await _executionService.RunAsync(tenantId, id, safeRequest, cancellationToken);
        return Ok(ApiResponse<AiWorkflowExecutionRunResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("executions/{execId}/cancel")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowExecute)]
    public async Task<ActionResult<ApiResponse<object>>> Cancel(string execId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _executionService.CancelAsync(tenantId, execId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = execId }, HttpContext.TraceIdentifier));
    }

    [HttpGet("executions/{execId}/progress")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<AiWorkflowExecutionProgressDto>>> GetProgress(string execId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _executionService.GetProgressAsync(tenantId, execId, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<AiWorkflowExecutionProgressDto>.Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "WorkflowInstanceNotFound"), HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<AiWorkflowExecutionProgressDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("executions/{execId}/nodes")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AiWorkflowNodeHistoryItem>>>> GetNodes(
        string execId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _executionService.GetNodeHistoryAsync(tenantId, execId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AiWorkflowNodeHistoryItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("node-types")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public ActionResult<ApiResponse<IReadOnlyList<AiWorkflowNodeTypeDto>>> GetNodeTypes()
    {
        var result = new List<AiWorkflowNodeTypeDto>
        {
            new("llm", "LLM", "AI", ApiResponseLocalizer.T(HttpContext, "AiWorkflowNodeTypeDescLlm")),
            new("plugin", "Plugin/API", "Integration", ApiResponseLocalizer.T(HttpContext, "AiWorkflowNodeTypeDescPlugin")),
            new("coderunner", "CodeRunner", "Compute", ApiResponseLocalizer.T(HttpContext, "AiWorkflowNodeTypeDescCodeRunner")),
            new("knowledgeretriever", "KnowledgeRetriever", "RAG", ApiResponseLocalizer.T(HttpContext, "AiWorkflowNodeTypeDescKnowledgeRetriever")),
            new("textprocessor", "TextProcessor", "Transform", ApiResponseLocalizer.T(HttpContext, "AiWorkflowNodeTypeDescTextProcessor")),
            new("httprequester", "HTTPRequester", "Integration", ApiResponseLocalizer.T(HttpContext, "AiWorkflowNodeTypeDescHttpRequester")),
            new("outputemitter", "OutputEmitter", "Output", ApiResponseLocalizer.T(HttpContext, "AiWorkflowNodeTypeDescOutputEmitter"))
        };
        return Ok(ApiResponse<IReadOnlyList<AiWorkflowNodeTypeDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/versions")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AiWorkflowVersionItem>>>> GetVersions(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var versions = await _designService.GetVersionsAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AiWorkflowVersionItem>>.Ok(versions, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/versions/diff")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowView)]
    public async Task<ActionResult<ApiResponse<AiWorkflowVersionDiff>>> GetVersionDiff(
        long id,
        [FromQuery] int from,
        [FromQuery] int to,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var diff = await _designService.GetVersionDiffAsync(tenantId, id, from, to, cancellationToken);
        if (diff is null)
        {
            return NotFound(ApiResponse<AiWorkflowVersionDiff>.Fail(
                ErrorCodes.NotFound,
                ApiResponseLocalizer.T(HttpContext, "WorkflowVersionPairNotFound", from, to),
                HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<AiWorkflowVersionDiff>.Ok(diff, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/versions/{version:int}/rollback")]
    [Authorize(Policy = PermissionPolicies.AiWorkflowUpdate)]
    public async Task<ActionResult<ApiResponse<AiWorkflowRollbackResult>>> Rollback(
        long id,
        int version,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<AiWorkflowRollbackResult>.Fail(
                ErrorCodes.Unauthorized,
                ApiResponseLocalizer.T(HttpContext, "Unauthorized"),
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _designService.RollbackAsync(tenantId, currentUser.UserId, id, version, cancellationToken);
        return Ok(ApiResponse<AiWorkflowRollbackResult>.Ok(result, HttpContext.TraceIdentifier));
    }
}
