using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Audit.Models;
using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using Atlas.WebApi.Helpers;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

/// <summary>
/// 系统参数管理（等保2.0：参数修改须有操作审计）
/// </summary>
[ApiController]
[Route("api/v1/system-configs")]
public sealed class SystemConfigsController : ControllerBase
{
    private readonly ISystemConfigQueryService _queryService;
    private readonly ISystemConfigCommandService _commandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IClientContextAccessor _clientContextAccessor;
    private readonly IAuditRecorder _auditRecorder;
    private readonly IValidator<SystemConfigCreateRequest> _createValidator;
    private readonly IValidator<SystemConfigUpdateRequest> _updateValidator;

    public SystemConfigsController(
        ISystemConfigQueryService queryService,
        ISystemConfigCommandService commandService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IClientContextAccessor clientContextAccessor,
        IAuditRecorder auditRecorder,
        IValidator<SystemConfigCreateRequest> createValidator,
        IValidator<SystemConfigUpdateRequest> updateValidator)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _clientContextAccessor = clientContextAccessor;
        _auditRecorder = auditRecorder;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.ConfigView)]
    public async Task<ActionResult<ApiResponse<PagedResult<SystemConfigDto>>>> Get(
        [FromQuery] PagedRequest request,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetSystemConfigsPagedAsync(tenantId, keyword, request.PageIndex, request.PageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<SystemConfigDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("by-key/{key}")]
    [Authorize(Policy = PermissionPolicies.ConfigView)]
    public async Task<ActionResult<ApiResponse<SystemConfigDto>>> GetByKey(string key, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetByKeyAsync(tenantId, key, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<SystemConfigDto>.Fail(ErrorCodes.NotFound, "参数不存在", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<SystemConfigDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.ConfigCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] SystemConfigCreateRequest request,
        CancellationToken cancellationToken)
    {
        _createValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var id = await _commandService.CreateSystemConfigAsync(tenantId, request, cancellationToken);

        await RecordAuditAsync("CREATE_SYSTEM_CONFIG", request.ConfigKey, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.ConfigUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] SystemConfigUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _updateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdateSystemConfigAsync(tenantId, id, request, cancellationToken);

        await RecordAuditAsync("UPDATE_SYSTEM_CONFIG", id.ToString(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.ConfigDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.DeleteSystemConfigAsync(tenantId, id, cancellationToken);

        await RecordAuditAsync("DELETE_SYSTEM_CONFIG", id.ToString(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    /// <summary>获取所有 FeatureFlag 类型的开关（无需权限，供前端 useFeatureFlag 使用）</summary>
    [HttpGet("feature-flags")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> GetFeatureFlags(CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var flags = await _queryService.GetFeatureFlagsAsync(tenantId, cancellationToken);
        var simplified = flags.ToDictionary(
            f => f.ConfigKey,
            f => new { f.ConfigValue, f.TargetJson, f.ConfigName });
        return Ok(ApiResponse<object>.Ok(simplified, HttpContext.TraceIdentifier));
    }

    private async Task RecordAuditAsync(string action, string target, CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null) return;

        var actor = string.IsNullOrWhiteSpace(currentUser.Username)
            ? currentUser.UserId.ToString()
            : currentUser.Username;
        var auditContext = new AuditContext(
            currentUser.TenantId,
            actor,
            action,
            "SUCCESS",
            target,
            ControllerHelper.GetIpAddress(HttpContext),
            ControllerHelper.GetUserAgent(HttpContext),
            _clientContextAccessor.GetCurrent());
        await _auditRecorder.RecordAsync(auditContext, cancellationToken);
    }
}
