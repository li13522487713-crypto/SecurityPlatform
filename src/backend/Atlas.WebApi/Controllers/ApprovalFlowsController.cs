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
    private readonly IApprovalDefinitionSemanticValidator _semanticValidator;
    private readonly IValidator<ApprovalFlowDefinitionCreateRequest> _createValidator;
    private readonly IValidator<ApprovalFlowDefinitionUpdateRequest> _updateValidator;

    public ApprovalFlowsController(
        IApprovalFlowQueryService queryService,
        IApprovalFlowCommandService commandService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IApprovalDefinitionSemanticValidator semanticValidator,
        IValidator<ApprovalFlowDefinitionCreateRequest> createValidator,
        IValidator<ApprovalFlowDefinitionUpdateRequest> updateValidator)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _semanticValidator = semanticValidator;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    /// <summary>
    /// 获取流程定义列表（分页）
    /// </summary>
    [HttpGet]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowView)]
    public async Task<ApiResponse<PagedResult<ApprovalFlowDefinitionListItem>>> GetPagedAsync(
        [FromQuery] PagedRequest request,
        [FromQuery] ApprovalFlowStatus? status = null,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetPagedAsync(tenantId, request, status, keyword, cancellationToken);
        return ApiResponse<PagedResult<ApprovalFlowDefinitionListItem>>.Ok(result, HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// 获取流程定义详情
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowView)]
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
    [Authorize(Policy = PermissionPolicies.ApprovalFlowView)]
    public async Task<ApiResponse<ApprovalFlowValidationResult>> ValidateAsync(
        [FromBody] ApprovalFlowDefinitionCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await _createValidator.ValidateAsync(request, cancellationToken);
        var errors = validation.Errors.Select(error => error.ErrorMessage).ToList();
        var details = validation.Errors
            .Select(error => new ApprovalFlowValidationIssue(
                "STRUCTURE_VALIDATION_ERROR",
                error.ErrorMessage,
                "error"))
            .ToList();

        if (errors.Count == 0)
        {
            var semanticIssues = _semanticValidator.Validate(request.DefinitionJson);
            details.AddRange(semanticIssues);
            errors.AddRange(semanticIssues
                .Where(issue => string.Equals(issue.Severity, "error", StringComparison.OrdinalIgnoreCase))
                .Select(issue => issue.Message));
        }

        var warnings = details
            .Where(issue => string.Equals(issue.Severity, "warning", StringComparison.OrdinalIgnoreCase))
            .Select(issue => issue.Message)
            .Distinct()
            .ToList();

        var payload = new ApprovalFlowValidationResult(
            errors.Count == 0,
            errors.Distinct().ToList(),
            warnings,
            details);
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
    /// 复制流程定义为新草稿
    /// </summary>
    [HttpPost("{id}/copy")]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowCreate)]
    public async Task<ApiResponse<ApprovalFlowDefinitionResponse>> CopyAsync(
        long id,
        [FromBody] ApprovalFlowCopyRequest? request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _commandService.CopyAsync(
            tenantId,
            id,
            request ?? new ApprovalFlowCopyRequest(null),
            cancellationToken);
        return ApiResponse<ApprovalFlowDefinitionResponse>.Ok(result, HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// 导入流程定义 JSON 为新草稿
    /// </summary>
    [HttpPost("import")]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowCreate)]
    public async Task<ApiResponse<ApprovalFlowDefinitionResponse>> ImportAsync(
        [FromBody] ApprovalFlowImportRequest request,
        CancellationToken cancellationToken = default)
    {
        var createRequest = new ApprovalFlowDefinitionCreateRequest
        {
            Name = request.Name,
            DefinitionJson = request.DefinitionJson,
            Description = request.Description,
            Category = request.Category,
            VisibilityScopeJson = request.VisibilityScopeJson,
            IsQuickEntry = request.IsQuickEntry ?? false
        };
        await _createValidator.ValidateAndThrowAsync(createRequest, cancellationToken);

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _commandService.ImportAsync(tenantId, request, cancellationToken);
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
        var definition = await _queryService.GetByIdAsync(currentUser.TenantId, id, cancellationToken);
        if (definition == null)
        {
            return ApiResponse<string>.Fail("NOT_FOUND", "流程定义不存在", HttpContext.TraceIdentifier);
        }

        var semanticIssues = _semanticValidator.Validate(definition.DefinitionJson);
        var blockingIssues = semanticIssues
            .Where(issue => string.Equals(issue.Severity, "error", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (blockingIssues.Length > 0)
        {
            var summary = string.Join("；", blockingIssues.Take(3).Select(issue => issue.Message));
            return ApiResponse<string>.Fail(
                "VALIDATION_ERROR",
                $"发布前校验不通过：{summary}",
                HttpContext.TraceIdentifier);
        }

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

    /// <summary>
    /// 导出流程定义 JSON
    /// </summary>
    [HttpGet("{id}/export")]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowView)]
    public async Task<ApiResponse<ApprovalFlowExportResponse>> ExportAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.ExportAsync(tenantId, id, cancellationToken);
        if (result == null)
        {
            return ApiResponse<ApprovalFlowExportResponse>.Fail(
                "NOT_FOUND",
                "流程定义不存在",
                HttpContext.TraceIdentifier);
        }

        return ApiResponse<ApprovalFlowExportResponse>.Ok(result, HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// 对比流程定义版本
    /// </summary>
    [HttpGet("{id}/versions/{targetVersion}/compare")]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowView)]
    public async Task<ApiResponse<ApprovalFlowCompareResponse>> CompareAsync(
        long id,
        int targetVersion,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.CompareAsync(tenantId, id, targetVersion, cancellationToken);
        if (result == null)
        {
            return ApiResponse<ApprovalFlowCompareResponse>.Fail(
                "NOT_FOUND",
                "目标版本不存在，无法对比",
                HttpContext.TraceIdentifier);
        }

        return ApiResponse<ApprovalFlowCompareResponse>.Ok(result, HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// 获取审批流版本历史列表
    /// </summary>
    [HttpGet("{id}/versions")]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowView)]
    public async Task<ApiResponse<IReadOnlyList<ApprovalFlowVersionListItem>>> GetVersionsAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var versions = await _queryService.GetVersionsAsync(tenantId, id, cancellationToken);
        return ApiResponse<IReadOnlyList<ApprovalFlowVersionListItem>>.Ok(versions, HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// 获取审批流指定版本详情
    /// </summary>
    [HttpGet("{id}/versions/{versionId:long}/detail")]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowView)]
    public async Task<ApiResponse<ApprovalFlowVersionDetail>> GetVersionDetailAsync(
        long id,
        long versionId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var version = await _queryService.GetVersionByIdAsync(tenantId, versionId, cancellationToken);
        if (version is null)
        {
            return ApiResponse<ApprovalFlowVersionDetail>.Fail("NOT_FOUND", "版本不存在", HttpContext.TraceIdentifier);
        }

        return ApiResponse<ApprovalFlowVersionDetail>.Ok(version, HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// 回滚审批流到指定版本
    /// </summary>
    [HttpPost("{id}/rollback/{versionId:long}")]
    [Authorize(Policy = PermissionPolicies.ApprovalFlowUpdate)]
    public async Task<ApiResponse<object>> RollbackAsync(
        long id,
        long versionId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return ApiResponse<object>.Fail(ErrorCodes.Unauthorized, "未登录", HttpContext.TraceIdentifier);
        }

        await _commandService.RollbackToVersionAsync(tenantId, id, versionId, currentUser.UserId, cancellationToken);
        return ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier);
    }

    /// <summary>弃用审批流定义 — 弃用后不允许新发起实例，但运行中实例可继续完成。</summary>
    [HttpPost("{id:long}/deprecate")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ApiResponse<object>> Deprecate(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return ApiResponse<object>.Fail(ErrorCodes.Unauthorized, "未登录", HttpContext.TraceIdentifier);
        }

        await _commandService.DeprecateAsync(tenantId, id, currentUser.UserId, cancellationToken);
        return ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier);
    }
}
