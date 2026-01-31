using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Models;
using Atlas.Application.Identity;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Enums;
using Atlas.WebApi.Authorization;
using FluentValidation;

namespace Atlas.WebApi.Controllers;

/// <summary>
/// 审批流定义管理控制器
/// </summary>
[ApiController]
[Route("api/v1/approval/flows")]
[Authorize]
public sealed class ApprovalFlowsController : ControllerBase
{
    private readonly IApprovalFlowQueryService _queryService;
    private readonly IApprovalFlowCommandService _commandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IValidator<ApprovalFlowDefinitionCreateRequest> _createValidator;
    private readonly IValidator<ApprovalFlowDefinitionUpdateRequest> _updateValidator;

    public ApprovalFlowsController(
        IApprovalFlowQueryService queryService,
        IApprovalFlowCommandService commandService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IValidator<ApprovalFlowDefinitionCreateRequest> createValidator,
        IValidator<ApprovalFlowDefinitionUpdateRequest> updateValidator)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    /// <summary>
    /// 获取流程定义列表（分页）
    /// </summary>
    [HttpGet]
    public async Task<ApiResponse<PagedResult<ApprovalFlowDefinitionListItem>>> GetPagedAsync(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] ApprovalFlowStatus? status = null,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var request = new PagedRequest(pageIndex, pageSize, keyword, null, false);
        var result = await _queryService.GetPagedAsync(tenantId, request, status, keyword, cancellationToken);
        return ApiResponse<PagedResult<ApprovalFlowDefinitionListItem>>.Ok(result, HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// 获取流程定义详情
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ApiResponse<ApprovalFlowDefinitionResponse>> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetByIdAsync(tenantId, id, cancellationToken);
        if (result == null)
        {
            return ApiResponse<ApprovalFlowDefinitionResponse>.Fail(
                "NOT_FOUND",
                "流程定义不存在",
                HttpContext.TraceIdentifier);
        }

        return ApiResponse<ApprovalFlowDefinitionResponse>.Ok(result, HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// 校验流程定义（不落库）
    /// </summary>
    [HttpPost("validation")]
    public async Task<ApiResponse<ApprovalFlowValidationResult>> ValidateAsync(
        [FromBody] ApprovalFlowDefinitionCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await _createValidator.ValidateAsync(request, cancellationToken);
        var errors = validation.Errors.Select(error => error.ErrorMessage).ToArray();
        var payload = new ApprovalFlowValidationResult(validation.IsValid, errors, Array.Empty<string>());
        return ApiResponse<ApprovalFlowValidationResult>.Ok(payload, HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// 创建流程定义
    /// </summary>
    [HttpPost]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowCreate)]
    public async Task<ApiResponse<ApprovalFlowDefinitionResponse>> CreateAsync(
        ApprovalFlowDefinitionCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _commandService.CreateAsync(tenantId, request, cancellationToken);
        return ApiResponse<ApprovalFlowDefinitionResponse>.Ok(result, HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// 更新流程定义
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowUpdate)]
    public async Task<ApiResponse<ApprovalFlowDefinitionResponse>> UpdateAsync(
        long id,
        [FromBody] ApprovalFlowDefinitionCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var updateRequest = new ApprovalFlowDefinitionUpdateRequest
        {
            Id = id,
            Name = request.Name,
            DefinitionJson = request.DefinitionJson,
            Description = request.Description,
            Category = request.Category,
            VisibilityScopeJson = request.VisibilityScopeJson,
            IsQuickEntry = request.IsQuickEntry
        };

        await _updateValidator.ValidateAndThrowAsync(updateRequest, cancellationToken);

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _commandService.UpdateAsync(tenantId, updateRequest, cancellationToken);
        return ApiResponse<ApprovalFlowDefinitionResponse>.Ok(result, HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// 发布流程定义
    /// </summary>
    [HttpPost("{id}/publication")]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowPublish)]
    public async Task<ApiResponse<string>> PublishAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();

        await _commandService.PublishAsync(
            currentUser.TenantId,
            id,
            currentUser.UserId,
            cancellationToken);
        return ApiResponse<string>.Ok("已发布", HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// 删除流程定义
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowDelete)]
    public async Task<ApiResponse<string>> DeleteAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.DeleteAsync(tenantId, id, cancellationToken);
        return ApiResponse<string>.Ok("已删除", HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// 禁用流程定义
    /// </summary>
    [HttpPost("{id}/deactivation")]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowDisable)]
    public async Task<ApiResponse<string>> DisableAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.DisableAsync(tenantId, id, cancellationToken);
        return ApiResponse<string>.Ok("已禁用", HttpContext.TraceIdentifier);
    }
}
