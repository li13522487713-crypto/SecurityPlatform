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
using Atlas.Presentation.Shared.Filters;

namespace Atlas.PlatformHost.Controllers;

[ApiController]
[Route("api/v1/ai-apps")]
[Authorize]
public sealed class AiAppsController : ControllerBase
{
    private readonly IAiAppService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IValidator<AiAppCreateRequest> _createValidator;
    private readonly IValidator<AiAppUpdateRequest> _updateValidator;
    private readonly IValidator<AiAppPublishRequest> _publishValidator;
    private readonly IValidator<AiAppResourceCopyRequest> _resourceCopyValidator;

    public AiAppsController(
        IAiAppService service,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IValidator<AiAppCreateRequest> createValidator,
        IValidator<AiAppUpdateRequest> updateValidator,
        IValidator<AiAppPublishRequest> publishValidator,
        IValidator<AiAppResourceCopyRequest> resourceCopyValidator)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _publishValidator = publishValidator;
        _resourceCopyValidator = resourceCopyValidator;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AiAppView)]
    public async Task<ActionResult<ApiResponse<PagedResult<AiAppListItem>>>> GetPaged(
        [FromQuery] PagedRequest request,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.GetPagedAsync(tenantId, keyword, request.PageIndex, request.PageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<AiAppListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AiAppView)]
    public async Task<ActionResult<ApiResponse<AiAppDetail>>> GetById(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.GetByIdAsync(tenantId, id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<AiAppDetail>.Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "AiAppDetailNotFound"), HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<AiAppDetail>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AiAppCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] AiAppCreateRequest request,
        CancellationToken cancellationToken)
    {
        _createValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var id = await _service.CreateAsync(tenantId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AiAppUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] AiAppUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _updateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _service.UpdateAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AiAppDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _service.DeleteAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/publish")]
    [Authorize(Policy = PermissionPolicies.AiAppPublish)]
    public async Task<ActionResult<ApiResponse<object>>> Publish(
        long id,
        [FromBody] AiAppPublishRequest request,
        CancellationToken cancellationToken)
    {
        _publishValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        await _service.PublishAsync(tenantId, id, currentUser.UserId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/publish-records")]
    [Authorize(Policy = PermissionPolicies.AiAppView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AiAppPublishRecordItem>>>> GetPublishRecords(
        long id,
        [FromQuery] int top = 20,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.GetPublishRecordsAsync(tenantId, id, top, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AiAppPublishRecordItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/conversation-templates")]
    [Authorize(Policy = PermissionPolicies.AiAppView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AiAppConversationTemplateListItem>>>> GetConversationTemplates(
        long id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.GetConversationTemplatesAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AiAppConversationTemplateListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/conversation-templates")]
    [Authorize(Policy = PermissionPolicies.AiAppUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> CreateConversationTemplate(
        long id,
        [FromBody] AiAppConversationTemplateCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var templateId = await _service.CreateConversationTemplateAsync(
            tenantId,
            id,
            currentUser.UserId,
            request,
            cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = templateId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}/conversation-templates/{templateId:long}")]
    [Authorize(Policy = PermissionPolicies.AiAppUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateConversationTemplate(
        long id,
        long templateId,
        [FromBody] AiAppConversationTemplateUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        await _service.UpdateConversationTemplateAsync(
            tenantId,
            id,
            templateId,
            currentUser.UserId,
            request,
            cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = templateId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}/conversation-templates/{templateId:long}")]
    [Authorize(Policy = PermissionPolicies.AiAppUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteConversationTemplate(
        long id,
        long templateId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        await _service.DeleteConversationTemplateAsync(
            tenantId,
            id,
            templateId,
            currentUser.UserId,
            cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = templateId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/version-check")]
    [Authorize(Policy = PermissionPolicies.AiAppView)]
    public async Task<ActionResult<ApiResponse<AiAppVersionCheckResult>>> CheckVersion(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.CheckVersionAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<AiAppVersionCheckResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/resource-copy-tasks")]
    [Authorize(Policy = PermissionPolicies.AiAppUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> SubmitResourceCopy(
        long id,
        [FromBody] AiAppResourceCopyRequest request,
        CancellationToken cancellationToken)
    {
        _resourceCopyValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var taskId = await _service.SubmitResourceCopyAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { TaskId = taskId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/resource-copy-tasks/latest")]
    [Authorize(Policy = PermissionPolicies.AiAppView)]
    public async Task<ActionResult<ApiResponse<AiAppResourceCopyTaskProgress>>> GetLatestResourceCopyTask(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.GetLatestResourceCopyProgressAsync(tenantId, id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<AiAppResourceCopyTaskProgress>.Fail(
                ErrorCodes.NotFound,
                ApiResponseLocalizer.T(HttpContext, "AiResourceCopyTaskNotFound"),
                HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<AiAppResourceCopyTaskProgress>.Ok(result, HttpContext.TraceIdentifier));
    }
}
