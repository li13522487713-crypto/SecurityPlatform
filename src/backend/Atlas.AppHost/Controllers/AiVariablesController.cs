using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Presentation.Shared.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/v1/ai-variables")]
[Authorize]
public sealed class AiVariablesController : ControllerBase
{
    private readonly IAiVariableService _service;
    private readonly IDagWorkflowQueryService _workflowQueryService;
    private readonly ITenantProvider _tenantProvider;
    private readonly IValidator<AiVariableCreateRequest> _createValidator;
    private readonly IValidator<AiVariableUpdateRequest> _updateValidator;

    public AiVariablesController(
        IAiVariableService service,
        IDagWorkflowQueryService workflowQueryService,
        ITenantProvider tenantProvider,
        IValidator<AiVariableCreateRequest> createValidator,
        IValidator<AiVariableUpdateRequest> updateValidator)
    {
        _service = service;
        _workflowQueryService = workflowQueryService;
        _tenantProvider = tenantProvider;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AiVariableView)]
    public async Task<ActionResult<ApiResponse<PagedResult<AiVariableListItem>>>> GetPaged(
        [FromQuery] PagedRequest request,
        [FromQuery] string? keyword = null,
        [FromQuery] AiVariableScope? scope = null,
        [FromQuery] long? scopeId = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.GetPagedAsync(
            tenantId,
            keyword,
            scope,
            scopeId,
            request.PageIndex,
            request.PageSize,
            cancellationToken);
        return Ok(ApiResponse<PagedResult<AiVariableListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AiVariableView)]
    public async Task<ActionResult<ApiResponse<AiVariableDetail>>> GetById(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.GetByIdAsync(tenantId, id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<AiVariableDetail>.Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "AiVariableNotFound"), HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<AiVariableDetail>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AiVariableCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] AiVariableCreateRequest request,
        CancellationToken cancellationToken)
    {
        _createValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var id = await _service.CreateAsync(tenantId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AiVariableUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] AiVariableUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _updateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _service.UpdateAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AiVariableDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _service.DeleteAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpGet("system-definitions")]
    [Authorize(Policy = PermissionPolicies.AiVariableView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AiSystemVariableDefinition>>>> GetSystemDefinitions(
        CancellationToken cancellationToken)
    {
        var result = await _service.GetSystemVariableDefinitionsAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AiSystemVariableDefinition>>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 工作流级变量树：返回当前节点上游可见的全局/系统/节点输出变量，供节点配置面板与 Prompt 编辑器消费。
    /// 路由放在 ai-variables 命名空间下与 system-definitions 保持一致，但语义上面向 workflow 编辑器。
    /// </summary>
    [HttpGet("workflows/{workflowId:long}/variable-tree")]
    [Authorize(Policy = PermissionPolicies.AiVariableView)]
    public async Task<ActionResult<ApiResponse<WorkflowVariableTreeDto>>> GetWorkflowVariableTree(
        long workflowId,
        [FromQuery] string? nodeKey,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _workflowQueryService.GetVariableTreeAsync(
            tenantId,
            workflowId,
            nodeKey,
            cancellationToken);
        return Ok(ApiResponse<WorkflowVariableTreeDto>.Ok(result, HttpContext.TraceIdentifier));
    }
}
