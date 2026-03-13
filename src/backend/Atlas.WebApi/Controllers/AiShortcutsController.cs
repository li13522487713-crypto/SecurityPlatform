using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/ai-shortcuts")]
[Authorize]
public sealed class AiShortcutsController : ControllerBase
{
    private readonly IAiShortcutCommandService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IValidator<AiShortcutCommandCreateRequest> _createValidator;
    private readonly IValidator<AiShortcutCommandUpdateRequest> _updateValidator;
    private readonly IValidator<AiBotPopupDismissRequest> _dismissValidator;

    public AiShortcutsController(
        IAiShortcutCommandService service,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IValidator<AiShortcutCommandCreateRequest> createValidator,
        IValidator<AiShortcutCommandUpdateRequest> updateValidator,
        IValidator<AiBotPopupDismissRequest> dismissValidator)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _dismissValidator = dismissValidator;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AiShortcutView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AiShortcutCommandItem>>>> GetCommands(
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.GetCommandsAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AiShortcutCommandItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AiShortcutManage)]
    public async Task<ActionResult<ApiResponse<object>>> CreateCommand(
        [FromBody] AiShortcutCommandCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        _createValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var id = await _service.CreateCommandAsync(tenantId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AiShortcutManage)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateCommand(
        long id,
        [FromBody] AiShortcutCommandUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        _updateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _service.UpdateCommandAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AiShortcutManage)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteCommand(
        long id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _service.DeleteCommandAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpGet("popup")]
    [Authorize(Policy = PermissionPolicies.AiShortcutView)]
    public async Task<ActionResult<ApiResponse<AiBotPopupInfoDto>>> GetPopup(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var result = await _service.GetPopupInfoAsync(tenantId, currentUser.UserId, cancellationToken);
        return Ok(ApiResponse<AiBotPopupInfoDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("popup/dismiss")]
    [Authorize(Policy = PermissionPolicies.AiShortcutView)]
    public async Task<ActionResult<ApiResponse<AiBotPopupInfoDto>>> DismissPopup(
        [FromBody] AiBotPopupDismissRequest request,
        CancellationToken cancellationToken = default)
    {
        _dismissValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var result = await _service.DismissPopupAsync(tenantId, currentUser.UserId, request, cancellationToken);
        return Ok(ApiResponse<AiBotPopupInfoDto>.Ok(result, HttpContext.TraceIdentifier));
    }
}
